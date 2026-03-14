using Npgsql;

namespace Infrastructure.State;

[GenerateSerializer]
public class TransactionContext
{
    [Id(0)]
    public required Guid Id { get; init; }

    [Id(1)]
    public Dictionary<Guid, IGrainTransactionHandler> Participants { get; } = new();

    [Id(20)]
    public string? ExceptionMessage { get; set; }
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
