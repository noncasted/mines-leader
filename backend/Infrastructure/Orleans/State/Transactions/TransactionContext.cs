using Orleans.Concurrency;

namespace Infrastructure.State;

[GenerateSerializer]
public class TransactionContext
{
    [Id(0)]
    public required Guid Id { get; init; }

    [Id(1)]
    public Dictionary<Guid, ITransactionParticipant> Participants { get; } = new();

    [Id(2)]
    public string? ExceptionMessage { get; set; }
}

public static class TransactionContextProvider
{
    private static readonly AsyncLocal<TransactionContext> _current = new();

    public static TransactionContext? Current => _current.Value;

    public static void SetCurrent(TransactionContext transactionContext)
    {
        _current.Value = transactionContext;
    }

    public static void Clear() => _current.Value = null;
}

public interface ITransactionParticipant : IGrainExtension
{
    [AlwaysInterleave]
    Task<IReadOnlyList<object>> CollectStates(Guid transactionId);

    [AlwaysInterleave]
    Task OnSuccess(Guid transactionId);

    [AlwaysInterleave]
    Task OnFailure(Guid transactionId);
}