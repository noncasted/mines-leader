using Orleans.Concurrency;

namespace Infrastructure;

public interface IGrainTransactionHandler : IGrainExtension
{
    [AlwaysInterleave]
    Task<Guid> Join(Guid transactionId);

    [AlwaysInterleave]
    Task<TransactionHandlerResult> CollectResult(Guid transactionId);

    // Called by Transactions.Process right before the database commit: fails when the transaction lost this grain
    // to a stuck-transaction takeover, so its old snapshot never overwrites the commit of the one that took over.
    [AlwaysInterleave]
    Task Seal(Guid transactionId);

    [AlwaysInterleave]
    Task OnSuccess(Guid transactionId);

    [AlwaysInterleave]
    Task OnFailure(Guid transactionId);
}
