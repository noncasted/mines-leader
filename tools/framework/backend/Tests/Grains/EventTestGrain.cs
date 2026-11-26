using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

public interface IEventTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task AppendEvents(int counterAmount, string label);

    [Transaction]
    Task AppendThenFail(int counterAmount);

    // Holds the transaction with the event applied and not yet written.
    [Transaction]
    Task AppendAndHold(int counterAmount, Guid gateId);

    Task AppendEventsStandalone(int counterAmount, string label);


    Task AppendWithoutRead();
    Task AppendNoApplyEvent();
    Task WriteWithoutPending();
    Task AppendTwiceInTransaction();
    Task<string> GetStreamId();
    Task<int> GetCounter();
    Task<EventTestAggregate> GetAggregate();
}

[Reentrant]
public class EventTestGrain : Grain, IEventTestGrain
{
    public EventTestGrain(
        [EventState] EventState<EventTestAggregate> state)
    {
        _state = state;
    }

    private readonly EventState<EventTestAggregate> _state;

    public async Task AppendEvents(int counterAmount, string label)
    {
        await _state.Read();
        await _state.Append(
            new CounterIncremented { Amount = counterAmount },
            new LabelChanged { Label = label }
        );
        await _state.Write();
    }

    public async Task AppendThenFail(int counterAmount)
    {
        await _state.Read();
        await _state.Append(new CounterIncremented { Amount = counterAmount });
        await _state.Write();
        throw new Exception("Intentional failure after append");
    }

    public async Task AppendAndHold(int counterAmount, Guid gateId)
    {
        await _state.Read();
        await _state.Append(new CounterIncremented { Amount = counterAmount });
        await TestGate.EnterAndWait(gateId);
        await _state.Write();
    }

    public async Task AppendEventsStandalone(int counterAmount, string label)
    {
        await _state.Read();
        await _state.Append(
            new CounterIncremented { Amount = counterAmount },
            new LabelChanged { Label = label }
        );
        await _state.Write();
    }

    public async Task AppendWithoutRead()
    {
        await _state.Append(new CounterIncremented { Amount = 1 });
    }

    public async Task AppendNoApplyEvent()
    {
        await _state.Read();
        await _state.Append(new NoApplyEvent { Value = 42 });
    }

    public async Task WriteWithoutPending()
    {
        await _state.Read();
        await _state.Write();
    }

    public async Task AppendTwiceInTransaction()
    {
        await _state.Read();
        await _state.Append(new CounterIncremented { Amount = 1 });
        await _state.Write();
        await _state.Append(new CounterIncremented { Amount = 2 });
        await _state.Write();
    }

    public Task<string> GetStreamId()
    {
        return Task.FromResult(_state.StreamId);
    }

    public async Task<int> GetCounter()
    {
        await _state.Read();
        return _state.Value.Counter;
    }

    public Task<EventTestAggregate> GetAggregate()
    {
        return _state.Read();
    }
}
