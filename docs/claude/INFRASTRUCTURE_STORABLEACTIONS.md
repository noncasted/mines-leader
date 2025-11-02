# Infrastructure: StorableActions

Module for persistent actions, batch processing, and distributed cluster state.

## Two Main Components

### BatchWriter - Batch Processing

Orleans grain for accumulating and processing entries in batches. Supports transactions.

```
Write(entry1) --+
Write(entry2) --+--> BatchWriterState.Entries ---> Process(batch) ---> Clear
Write(entry3) --+         (accumulated)              (execute)
```

### ClusterState - Distributed State

Shared state across all services. Changes are automatically propagated via messaging.

```
Service A: SetValue(state) ---> ClusterStateStorage grain ---> MessageQueue
                                        |                          |
                                        v                          v
                                   PostgreSQL              All services update
                                                           local ClusterState<T>
```

## BatchWriter

### Base Class

```csharp
public abstract class BatchWriter<TState, TEntry> : CommonGrain, ITransactionHook, IBatchWriter<TEntry>
    where TState : BatchWriterState<TEntry>
{
    protected abstract BatchWriterOptions Options { get; }
    protected abstract Task Process(IReadOnlyList<TEntry> entries);
}

public class BatchWriterOptions {
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
    public bool RequiresTransaction { get; set; } = true;
}
```

### Write Methods

```csharp
// Within Orleans transaction - entry added only on commit
await batcher.WriteTransactional(entry);

// Immediate write to state
await batcher.WriteDirect(entry);
```

### How It Works

1. `WriteTransactional` adds entry to `_pending[transactionId]`
2. On transaction success (`OnSuccess`) - entries move to `State.Entries`
3. `TaskScheduler` schedules `Loop()` execution
4. `Loop()` calls `Process(entries)` then clears state
5. On error - task is rescheduled for retry

### Transaction Hook

BatchWriter implements `ITransactionHook`:

```csharp
public Task OnSuccess(Guid transactionId) {
    _state.State.Entries.AddRange(_pending[transactionId]);
    _pending.Remove(transactionId);
    await _state.WriteStateAsync();
    _taskScheduler.Schedule(_task);
}

public Task OnFailure(Guid transactionId) {
    _pending.Remove(transactionId);
}
```

### Example: MessageQueue

```csharp
public class MessageQueue : BatchWriter<MessageQueueState, object>, IMessageQueue {
    protected override BatchWriterOptions Options { get; } = new() {
        RequiresTransaction = false  // Messages don't require transactions
    };

    protected override async Task Process(IReadOnlyList<object> entries) {
        await Task.WhenAll(_observers.Values.Select(o => o.Observer.Send(entries)));
    }
}
```

## ClusterState

### Server Side (Grain)

```csharp
public class ClusterStateStorage<T> : Grain, IClusterStateStorage<T> {
    public Task Set(T value) {
        _state.State = value;
        return Task.WhenAll(
            _state.WriteStateAsync(),
            _messaging.PushDirectQueue(new ClusterStateMessageQueueId<T>(), value!)
        );
    }

    public ValueTask<T> Get() {
        return ValueTask.FromResult(_state.State);
    }
}
```

### Client Side (Local Service)

```csharp
public class ClusterState<T> : ViewableProperty<T>, ILocalSetupCompleted, IClusterState<T> {
    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime) {
        // Subscribe to updates
        _messaging.ListenQueue<T>(lifetime, new ClusterStateMessageQueueId<T>(), OnUpdate);

        // Load initial value
        var currentValue = await _orleans.GetClusterState<T>();
        Set(currentValue);
    }

    public Task SetValue(T value) {
        Set(value);
        return _orleans.SetClusterState(value);
    }
}
```

### Usage

```csharp
// Registration
builder.AddClusterState<MyClusterConfig>();

// Inject and use
public class MyService {
    public MyService(IClusterState<MyClusterConfig> config) {
        // Read current value
        var current = config.Value;

        // Subscribe to changes
        config.View(lifetime, newValue => {
            Console.WriteLine($"Config updated: {newValue}");
        });

        // Update value (propagates to all services)
        await config.SetValue(new MyClusterConfig { ... });
    }
}
```

### Example: ClusterFeatures

```csharp
public class ClusterFeatures : ClusterState<ClusterFeaturesState>, IClusterFeatures {
    public IViewableProperty<bool> AcceptingConnections => _acceptingConnections;

    protected override void OnSetup(IReadOnlyLifetime lifetime) {
        this.View(lifetime, value => {
            _acceptingConnections.Set(value.AcceptingConnections);
        });
    }

    public Task SetAcceptingConnections(bool accepting) {
        Value.AcceptingConnections = accepting;
        return SetValue(Value);
    }
}
```

## BatchWritersWakeUp

At cluster startup, Coordinator wakes up all BatchWriters to continue processing:

```csharp
public class BatchWritersWakeUp {
    private readonly IReadOnlyDictionary<string, string> _stateToNamespace = new Dictionary<string, string>() {
        { States.Messaging_Queue, typeof(MessageQueue).FullName! }
    };

    public async Task Execute() {
        foreach (var (state, grainNamespace) in _stateToNamespace) {
            var reader = _orleans.CreateDbReader(state)
                .WhereType(state)
                .SelectExtension();

            await foreach (var entry in reader.Read()) {
                var grain = _orleans.Grains.GetGrain<IBatchWriter>(entry.Extension, grainNamespace);
                await grain.Start();
            }
        }
    }
}
```

## Rules

1. **BatchWriter state storage** - defined in `Common/Extensions/States.cs`
2. **ClusterState queue ID** - `cluster-state-{TypeFullName}`
3. **BatchWriter retry** - on Process error, task is rescheduled
4. **Transaction safety** - `WriteTransactional` only commits with Orleans transaction

## Logging

| Tag | Component | Description |
|-----|-----------|-------------|
| `[BatchWriter]` | BatchWriter | Batch processing |
| `[Coordinator] [WakeUp]` | BatchWritersWakeUp | Startup wake-up |

## Key Files

| File | Purpose |
|------|---------|
| `StorableActions/Batchers/Grains/BatchWriter.cs` | BatchWriter base class |
| `StorableActions/Batchers/Interfaces/IBatchWriter.cs` | Interface |
| `StorableActions/Batchers/Interfaces/BatchWriterState.cs` | State class |
| `StorableActions/ClusterStates/Grains/ClusterStateStorage.cs` | ClusterState grain |
| `StorableActions/ClusterStates/Service/ClusterState.cs` | ClusterState client |
| `Coordination/Coordinator/BatchWritersWakeUp.cs` | Startup wake-up |
