using Common.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.State;

public class TransactionResult
{
    public required bool IsSuccess { get; init; }
}

public class TransactionParameters
{
    public required Func<Task> Action { get; init; }
    public required List<Func<NpgsqlTransaction, Task>> Callbacks { get; init; }
}

public class TransactionCommitResult
{
    public required IReadOnlyList<GrainStateRecord> States { get; init; }
    public required IReadOnlyList<ISideEffect> SideEffects { get; init; }
}

public interface ITransactions
{
    Task<TransactionResult> Run(TransactionParameters action);
}

public class Transactions : ITransactions
{
    public Transactions(
        IGrainStateStorage stateStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        ILogger<Transactions> logger)
    {
        _stateStorage = stateStorage;
        _dbSource = dbSource;
        _sideEffectsStorage = sideEffectsStorage;
        _logger = logger;
    }

    private readonly IGrainStateStorage _stateStorage;
    private readonly IDbSource _dbSource;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly ILogger<Transactions> _logger;

    public async Task<TransactionResult> Run(TransactionParameters parameters)
    {
        var context = new TransactionContext
        {
            Id = Guid.NewGuid()
        };

        TransactionContextProvider.SetCurrent(context);

        try
        {
            await parameters.Action();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] In action exception occured during transaction {TransactionId}",
                context.Id
            );

            await Rollback(context);

            return new TransactionResult
            {
                IsSuccess = false
            };
        }

        TransactionCommitResult result;

        try
        {
            result = await CollectStates();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to collect commit result during transaction {TransactionId}",
                context.Id
            );

            await Rollback(context);

            return new TransactionResult
            {
                IsSuccess = false
            };
        }

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                if (result.States.Count != 0)
                    await _stateStorage.Write(transaction, result.States);

                if (result.SideEffects.Count != 0)
                    await _sideEffectsStorage.Write(transaction, result.SideEffects);

                foreach (var callback in parameters.Callbacks)
                    await callback(transaction);

                await transaction.CommitAsync();

                var confirmTasks = context.Participants.Select(t => t.Value.OnSuccess(context.Id));
                await Task.WhenAll(confirmTasks);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "[Transaction] [Error] Failed to record changes during transaction {TransactionId}",
                    context.Id
                );
                
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to commit to db during transaction {TransactionId}",
                context.Id
            );
            
            await Rollback(context);

            return new TransactionResult
            {
                IsSuccess = false
            };
        }

        return new TransactionResult
        {
            IsSuccess = true
        };

        async Task<TransactionCommitResult> CollectStates()
        {
            var states = new List<GrainStateRecord>();
            var sideEffects = new List<ISideEffect>();

            var collections = await Task.WhenAll(context.Participants.Select(p => Collect(p.Value)));

            foreach (var collection in collections)
            {
                states.AddRange(collection.States);
                sideEffects.AddRange(collection.SideEffects);
            }

            return new TransactionCommitResult()
            {
                States = states,
                SideEffects = sideEffects
            };
            ;

            async Task<TransactionCommitResult> Collect(IGrainTransactionHandler handler)
            {
                var grainStates = new List<GrainStateRecord>();

                var result = await handler.CollectResult(context.Id);
                var participantId = handler.GetGrainId();

                foreach (var state in result.States)
                {
                    grainStates.Add(new GrainStateRecord
                        {
                            Id = participantId,
                            Value = state
                        }
                    );
                }

                return new TransactionCommitResult
                {
                    States = grainStates,
                    SideEffects = result.SideEffects
                };
            }
        }
    }

    private async Task Rollback(TransactionContext context)
    {
        foreach (var (_, participant) in context.Participants)
        {
            try
            {
                await participant.OnFailure(context.Id);

            }
            catch (Exception e)
            {

            }
        }
    }
}