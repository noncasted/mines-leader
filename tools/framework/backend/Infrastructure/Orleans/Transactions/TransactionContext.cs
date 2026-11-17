using System.Collections.Concurrent;

namespace Infrastructure;

[GenerateSerializer]
public class TransactionContext
{
    [Id(0)]
    public required Guid Id { get; init; }

    [Id(1)]
    public ConcurrentDictionary<Guid, IGrainTransactionHandler> Participants { get; } = new();

    [Id(2)]
    public ConcurrentDictionary<Guid, ISideEffect> SideEffects { get; } = new();

    // participantId → snapshot of the participant's states/events taken when its [Transaction] method returned.
    // Travels back to Transactions.Process inside TransactionResponse, so CollectResult is not a separate RPC.
    // [Id(20)] was the exception message: not to be used again.
    [Id(3)]
    public ConcurrentDictionary<Guid, TransactionHandlerResult> Results { get; } = new();

    // Keeps the newest snapshot per participant. Sequence is assigned by the participant's handler,
    // so a snapshot from a slower parallel branch cannot overwrite a newer one.
    public void AddResult(Guid participantId, TransactionHandlerResult result)
    {
        Results.AddOrUpdate(participantId, result, (_, existing) => result.Sequence > existing.Sequence ? result : existing);
    }
}
