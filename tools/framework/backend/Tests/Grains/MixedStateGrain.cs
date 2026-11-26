using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

public interface IMixedStateGrain : IGrainWithGuidKey
{
    [Transaction]
    Task IncrementAndAppend(int amount);

    [Transaction]
    Task IncrementAndAppendThenFail(int amount);

    Task<int> GetDirectValue();
    Task<string> GetStreamId();
}

[Reentrant]
public class MixedStateGrain : Grain, IMixedStateGrain
{
    public MixedStateGrain(
        [State] State<TxTestState> directState,
        [EventState] EventState<EventTestAggregate> eventState)
    {
        _directState = directState;
        _eventState = eventState;
    }

    private readonly State<TxTestState> _directState;
    private readonly EventState<EventTestAggregate> _eventState;

    public async Task IncrementAndAppend(int amount)
    {
        await _directState.Write(s => s.Increment());
        await _eventState.Read();
        await _eventState.Append(new CounterIncremented { Amount = amount });
        await _eventState.Write();
    }

    public async Task IncrementAndAppendThenFail(int amount)
    {
        await _directState.Write(s => s.Increment());
        await _eventState.Read();
        await _eventState.Append(new CounterIncremented { Amount = amount });
        await _eventState.Write();
        throw new Exception("Intentional mixed failure");
    }

    public async Task<int> GetDirectValue()
    {
        return await _directState.Read(s => s.Value);
    }

    public Task<string> GetStreamId()
    {
        return Task.FromResult(_eventState.StreamId);
    }
}
