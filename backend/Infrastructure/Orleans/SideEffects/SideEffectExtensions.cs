using Infrastructure.State;

namespace Infrastructure;

public static class SideEffectExtensions
{
    public static void RegisterSideEffect(this Grain grain, ISideEffect sideEffect)
    {
        if (TransactionContextProvider.Current == null)
            throw new InvalidOperationException("Side effects can only be registered within a transaction.");

        var handler = (GrainTransactionHandler)grain.GrainContext.GetComponent<IGrainTransactionHandler>()!;
        handler.RecordSideEffect(sideEffect);
    }
}