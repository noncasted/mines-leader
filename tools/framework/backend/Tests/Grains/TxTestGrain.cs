using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

public interface ITxTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task Increment();

    [Transaction]
    Task IncrementWithDelay(int delayMs);

    [Transaction]
    Task IncrementAndHold(Guid gateId);

    Task IncrementDirect();

    Task<Guid> GetActivationId();

    Task<int> Get();
    Task Deactivate();
}

[Reentrant]
public class TxTestGrain : Grain, ITxTestGrain
{
    public TxTestGrain([State] State<TxTestState> state)
    {
        _state = state;
    }

    private readonly State<TxTestState> _state;
    private readonly Guid _activationId = Guid.NewGuid();

    public Task Increment()
    {
        return _state.Write(s => s.Increment());
    }

    public async Task IncrementWithDelay(int delayMs)
    {
        await _state.Write(s => s.Increment());
        await Task.Delay(delayMs);
    }

    public async Task IncrementAndHold(Guid gateId)
    {
        await _state.Write(s => s.Increment());
        await TestGate.EnterAndWait(gateId);
    }

    public Task IncrementDirect()
    {
        return _state.Write(s => s.Increment());
    }

    public Task<Guid> GetActivationId()
    {
        return Task.FromResult(_activationId);
    }

    public async Task<int> Get()
    {
        return await _state.Read(s => s.Value);
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}
