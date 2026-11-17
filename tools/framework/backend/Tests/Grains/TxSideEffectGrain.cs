using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

// Registers side effects within a transaction via AddToTransaction.
public interface ITxSideEffectGrain : IGrainWithGuidKey
{
    [Transaction]
    Task IncrementAndRegisterSideEffect(Guid sideEffectTargetId);

    [Transaction]
    Task RegisterMultipleSideEffects(IReadOnlyList<Guid> targetIds);

    [Transaction]
    Task<int> IncrementAndReturn();

    Task<int> Get();
}

[Reentrant]
public class TxSideEffectGrain : Grain, ITxSideEffectGrain
{
    public TxSideEffectGrain([State] State<TxTestState> state)
    {
        _state = state;
    }

    private readonly State<TxTestState> _state;

    public async Task IncrementAndRegisterSideEffect(Guid sideEffectTargetId)
    {
        await _state.Write(s => s.Increment());
        new TestSideEffect { TargetGrainId = sideEffectTargetId }.AddToTransaction();
    }

    public async Task RegisterMultipleSideEffects(IReadOnlyList<Guid> targetIds)
    {
        await _state.Write(s => s.Increment());

        foreach (var targetId in targetIds)
            new TestSideEffect { TargetGrainId = targetId }.AddToTransaction();
    }

    public async Task<int> IncrementAndReturn()
    {
        var state = await _state.Update(s => s.Increment());
        return state.Value;
    }

    public async Task<int> Get()
    {
        return await _state.Read(s => s.Value);
    }
}
