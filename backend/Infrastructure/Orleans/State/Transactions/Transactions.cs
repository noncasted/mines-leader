namespace Infrastructure.State;

public class TransactionResult
{
    public required bool IsSuccess { get; init; }
}

public interface ITransactions
{
    Task<TransactionResult> Run(Func<Task> action);
}

public class Transactions : ITransactions
{
    public Transactions(IGrainStateStorage storage)
    {
        _storage = storage;
    }

    private readonly IGrainStateStorage _storage;

    public async Task<TransactionResult> Run(Func<Task> action)
    {
        var context = new TransactionContext
        {
            Id = Guid.NewGuid()
        };

        TransactionContextProvider.SetCurrent(context);

        try
        {
            await action();
            var states = await CollectStates();

            await _storage.Write(states);

            var confirmTasks = context.Participants.Select(t => t.Value.OnSuccess(context.Id));
            await Task.WhenAll(confirmTasks);
        }
        catch (Exception e)
        {
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

        async Task<IReadOnlyList<(GrainId id, object value)>> CollectStates()
        {
            var states = new List<(GrainId id, object value)>();

            var collections = await Task.WhenAll(context.Participants.Select(p => Collect(p.Value)));

            foreach (var collection in collections)
                states.AddRange(collection);

            return states;

            async Task<IReadOnlyList<(GrainId id, object value)>> Collect(IGrainTransactionHandler handler)
            {
                var collection = new List<(GrainId id, object value)>();

                var participantStates = await handler.CollectStates(context.Id);
                var participantId = handler.GetGrainId();

                foreach (var state in participantStates)
                    collection.Add((participantId, state));

                return collection;
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