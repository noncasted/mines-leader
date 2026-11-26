using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

public interface ISideEffectTestGrain : IGrainWithGuidKey
{
    Task RegisterSideEffect(Guid targetId);
}

public class SideEffectTestGrain : Grain, ISideEffectTestGrain
{
    public SideEffectTestGrain(
        [State] State<TxTestState> state,
        ISideEffectsStorage sideEffectsStorage)
    {
        _state = state;
        _sideEffectsStorage = sideEffectsStorage;
    }

    private readonly State<TxTestState> _state;
    private readonly ISideEffectsStorage _sideEffectsStorage;

    public async Task RegisterSideEffect(Guid targetId)
    {
        await _sideEffectsStorage.Write(new TestSideEffect { TargetGrainId = targetId });
    }
}
