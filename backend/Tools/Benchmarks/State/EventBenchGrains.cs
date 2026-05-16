using Common;
using Infrastructure;
using Infrastructure.State;

namespace Benchmarks;

[GenerateSerializer]
public class CounterIncremented
{
    [Id(0)] public int Amount { get; set; }
}

[GenerateSerializer]
[GrainEventState(State = "event_bench", Lookup = "EventBench", Key = GrainKeyType.Guid)]
public class EventBenchAggregate : IEventStateValue
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public int Counter { get; set; }
    public int Version => 0;

    public void Apply(CounterIncremented e) => Counter += e.Amount;
}

public interface IEventStorageTestGrain : IGrainWithGuidKey
{
    Task Append(int amount);
    Task Deactivate();
}

[GrainType("bench-event-storage")]
public class EventStorageTestGrain : Grain, IEventStorageTestGrain
{
    public EventStorageTestGrain(IEventStorage storage)
    {
        _storage = storage;
    }

    private readonly IEventStorage _storage;
    private string StreamId => $"event_bench:{this.GetPrimaryKey():D}";

    public async Task Append(int amount)
    {
        _ = await _storage.Read<EventBenchAggregate>(StreamId);
        await _storage.Append(StreamId, new CounterIncremented { Amount = amount });
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}

public interface IEventStateTestGrain : IGrainWithGuidKey
{
    Task Append(int amount);
    Task Deactivate();
}

[GrainType("bench-event-state")]
public class EventStateTestGrain : Grain, IEventStateTestGrain
{
    public EventStateTestGrain([EventState] EventState<EventBenchAggregate> state)
    {
        _state = state;
    }

    private readonly EventState<EventBenchAggregate> _state;

    public async Task Append(int amount)
    {
        await _state.Read();
        await _state.Append(new CounterIncremented { Amount = amount });
        await _state.Write();
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}

public interface IEventStateTransactionTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task Append(int amount);
    Task Deactivate();
}

[GrainType("bench-event-state-tx")]
public class EventStateTransactionTestGrain : Grain, IEventStateTransactionTestGrain
{
    public EventStateTransactionTestGrain([EventState] EventState<EventBenchAggregate> state)
    {
        _state = state;
    }

    private readonly EventState<EventBenchAggregate> _state;

    public async Task Append(int amount)
    {
        await _state.Read();
        await _state.Append(new CounterIncremented { Amount = amount });
        await _state.Write();
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}
