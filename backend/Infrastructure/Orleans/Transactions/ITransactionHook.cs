using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure;

public interface ITransactionHook : IAddressable
{
    Task OnSuccess(Guid transactionId);
    Task OnFailure(Guid transactionId);
}

public static class TransactionHookExtensions
{
    public static Task AddTransactionHook(this IGrainFactory grains, ITransactionHook hook)
    {
        var transactionId = TransactionContext.GetRequiredTransactionInfo().TransactionId;
        var handle = grains.GetGrain<ITransactionHandle>(transactionId);
        return handle.AddListener(hook);
    }

    public static Task AsTransactionHook<T>(this T grain) where T : ITransactionHook, ICommonGrain
    {
        var transactionId = TransactionContext.GetRequiredTransactionInfo().TransactionId;
        var handle = grain.Grains.GetGrain<ITransactionHandle>(transactionId);
        var info = TransactionContext.GetTransactionInfo();
        var participantId = new ParticipantId("hook", grain.Reference, ParticipantId.Role.Resource);
        info.Participants.TryAdd(participantId, new AccessCounter());

        return handle.AddListener(grain);
    }
}