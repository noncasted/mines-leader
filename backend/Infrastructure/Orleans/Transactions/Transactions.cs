using System.Collections.Concurrent;
using System.Diagnostics;
using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure;

public class TransactionResult
{
    public required bool IsSuccess { get; init; }
    public Exception? Error { get; init; }

    public override string ToString() => Error != null
        ? $"TransactionResult(Success={IsSuccess}, Error={Error})"
        : $"TransactionResult(Success={IsSuccess})";
}

public class TransactionParameters
{
    public required Func<Task> Action { get; init; }
    public required List<Func<NpgsqlTransaction, Task>> Callbacks { get; init; }
}

public class TransactionCommitResult
{
    public required IReadOnlyList<GrainStateRecord> States { get; init; }
    public required List<GrainEventRecord> Events { get; init; }
}

public interface ITransactions
{
    Task<TransactionResult> Process(TransactionParameters action);
}

public class Transactions : ITransactions
{
    public Transactions(
        IStateStorage stateStorage,
        IEventStorage eventStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        ILogger<Transactions> logger)
    {
        _stateStorage = stateStorage;
        _eventStorage = eventStorage;
        _dbSource = dbSource;
        _sideEffectsStorage = sideEffectsStorage;
        _logger = logger;
    }

    private readonly IStateStorage _stateStorage;
    private readonly IEventStorage _eventStorage;
    private readonly IDbSource _dbSource;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly ILogger<Transactions> _logger;

    public async Task<TransactionResult> Process(TransactionParameters parameters)
    {
        using var activity = TraceExtensions.Transactions.StartActivity("Transaction.Process");
        using var watch = MetricWatch.Start(BackendMetrics.TransactionDuration);

        var context = new TransactionContext
        {
            Id = Guid.NewGuid()
        };

        activity?.SetTag("transaction.id", context.Id);

        TransactionContextProvider.SetCurrent(context);

        try
        {
            await parameters.Action();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] In action exception occured during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
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
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        activity?.SetTag("participant.count", context.Participants.Count);
        BackendMetrics.TransactionParticipantCount.Record(context.Participants.Count);

        try
        {
            await Commit(result, context, parameters.Callbacks);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to commit to db during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        // The changes are in the database from here on: the result is success whatever the participants do.
        // Rolling back (OnFailure) after the commit would desync grain memory from the database, and a failure
        // result would make the caller retry an operation that is already done.
        await ConfirmParticipants(context);

        BackendMetrics.TransactionTotal.Add(1);
        BackendMetrics.TransactionSuccess.Add(1);

        return new TransactionResult
        {
            IsSuccess = true
        };

        async Task<TransactionCommitResult> CollectStates()
        {
            var states = new List<GrainStateRecord>();
            var events = new List<GrainEventRecord>();

            var collections = await Task.WhenAll(context.Participants.Select(p => Collect(p.Key, p.Value)));

            foreach (var collection in collections)
            {
                states.AddRange(collection.States);
                events.AddRange(collection.Events);
            }

            return new TransactionCommitResult()
            {
                States = states,
                Events = events
            };

            async Task<TransactionCommitResult> Collect(Guid handlerId, IGrainTransactionHandler handler)
            {
                var grainStates = new List<GrainStateRecord>();
                var grainEvents = new List<GrainEventRecord>();

                // The snapshot normally arrives inside TransactionResponse (TransactionRequestBase.Invoke);
                // the RPC is only for participants whose response did not reach this context.
                var result = context.Results.TryGetValue(handlerId, out var snapshot)
                    ? snapshot
                    : await handler.CollectResult(context.Id);
                var participantId = handler.GetGrainId();

                foreach (var state in result.States)
                {
                    grainStates.Add(new GrainStateRecord
                    {
                        Id = participantId,
                        Value = state
                    });
                }

                grainEvents.AddRange(result.Events);

                return new TransactionCommitResult
                {
                    States = grainStates,
                    Events = grainEvents
                };
            }
        }
    }

    private async Task Commit(
        TransactionCommitResult result,
        TransactionContext context,
        List<Func<NpgsqlTransaction, Task>> callbacks)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Direct states and side effects go to Postgres as one NpgsqlBatch: one round trip
            // for the whole transaction instead of one per table group plus one for side effects.
            await WriteBatch(connection, transaction, result.States, context.SideEffects);

            // Marten runs its own pipeline inside the same transaction; it cannot join the batch.
            if (result.Events.Count != 0)
                await _eventStorage.Write(transaction, result.Events);

            foreach (var callback in callbacks)
                await callback(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to record changes during transaction {TransactionId}",
                context.Id);

            await transaction.RollbackAsync();
            throw;
        }

        // A failed commit is rolled back by the server; there is nothing left to roll back here.
        await transaction.CommitAsync();
    }

    private async Task ConfirmParticipants(TransactionContext context)
    {
        var confirmations = context.Participants.Select(async participant => {
            try
            {
                await participant.Value.OnSuccess(context.Id);
            }
            catch (Exception e)
            {
                // Not a transaction failure: the data is committed. A handler that threw has released its grain
                // already (GrainTransactionHandler.OnSuccess); one the call did not reach keeps the lock until
                // the stuck-transaction takeover in GrainTransactionHandler.Join reloads its state.
                _logger.LogError(e,
                    "[Transaction] [Error] Participant {ParticipantId} failed to confirm committed transaction {TransactionId}",
                    participant.Key, context.Id);

                BackendMetrics.TransactionConfirmFailure.Add(1);
            }
        });

        await Task.WhenAll(confirmations);
    }

    private async Task WriteBatch(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<GrainStateRecord> states,
        ConcurrentDictionary<Guid, ISideEffect> sideEffects)
    {
        var commands = new List<NpgsqlBatchCommand>();

        if (states.Count != 0)
            commands.AddRange(_stateStorage.BuildWriteCommands(states));

        if (sideEffects.Count != 0)
            commands.Add(_sideEffectsStorage.BuildWriteCommand(sideEffects.Values.ToList()));

        if (commands.Count == 0)
            return;

        // Same metrics DirectStorage.Write would have recorded for the transactional write.
        var start = Stopwatch.GetTimestamp();

        try
        {
            await using var batch = new NpgsqlBatch(connection, transaction);

            foreach (var command in commands)
                batch.BatchCommands.Add(command);

            await batch.ExecuteNonQueryAsync();
        }
        finally
        {
            if (states.Count != 0)
            {
                BackendMetrics.StateWriteDuration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                BackendMetrics.StateWriteTotal.Add(1);
                BackendMetrics.StateWriteBatchSize.Record(states.Count);
            }
        }
    }

    private async Task Rollback(TransactionContext context, Activity? activity = null)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Transaction rolled back");
        BackendMetrics.TransactionRollback.Add(1);

        foreach (var (participantId, participant) in context.Participants)
        {
            try
            {
                await participant.OnFailure(context.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Transactions] Failed to rollback participant {ParticipantId}", participantId);
                BackendMetrics.TransactionRollbackFailure.Add(1);
            }
        }
    }
}