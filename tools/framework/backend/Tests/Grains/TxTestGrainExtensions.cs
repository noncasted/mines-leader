using Framework.Tests.Fixtures;

namespace Framework.Tests.Grains;

public static class TxTestGrainExtensions
{
    // Returns when a new activation answers: its state comes from the database, not from the memory of the old one.
    public static async Task DeactivateAndWaitNewActivation(this ITxTestGrain grain)
    {
        var activation = await grain.GetActivationId();
        await grain.Deactivate();
        await TestWait.Until(async () => await grain.GetActivationId() != activation);
    }

    public static async Task DeactivateAndWaitNewActivation(this ITxRequestKindsGrain grain)
    {
        var activation = await grain.GetActivationId();
        await grain.Deactivate();
        await TestWait.Until(async () => await grain.GetActivationId() != activation);
    }
}
