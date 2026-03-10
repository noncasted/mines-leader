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
            var states = CollectStates();

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

        IReadOnlyList<(GrainId id, object value)> CollectStates()
        {
            var states = new List<(GrainId id, object value)>();

            foreach (var (_, participant) in context.Participants)
            {
                var participantStates = participant.CollectStates(context.Id).Result;
                var participantId = participant.GetGrainId();

                foreach (var state in participantStates)
                    states.Add((participantId, state));
            }

            return states;
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