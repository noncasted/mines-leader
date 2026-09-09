using System.Collections.Concurrent;
using Npgsql;

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
    [Id(3)]
    public ConcurrentDictionary<Guid, TransactionHandlerResult> Results { get; } = new();

    [Id(20)]
    public string? ExceptionMessage { get; set; }

    // Keeps the newest snapshot per participant. Sequence is assigned by the participant's handler,
    // so a snapshot from a slower parallel branch cannot overwrite a newer one.
    public void AddResult(Guid participantId, TransactionHandlerResult result)
    {
        Results.AddOrUpdate(participantId, result, (_, existing) => result.Sequence > existing.Sequence ? result : existing);
    }
}

public static class TransactionContextProvider
{
    private static readonly AsyncLocal<TransactionContext?> _current = new();

    // Not part of TransactionContext to avoid polluting [GenerateSerializer] class with non-serializable types.
    private static readonly AsyncLocal<List<Func<NpgsqlTransaction, Task>>?> _callbacks = new();

    public static TransactionContext? Current => _current.Value;

    public static void SetCurrent(TransactionContext transactionContext)
    {
        _current.Value = transactionContext;
        _callbacks.Value = new List<Func<NpgsqlTransaction, Task>>();
    }

    public static void Clear()
    {
        _current.Value = null;
        _callbacks.Value = null;
    }
}