# Orleans

Distributed actor system for stateful service orchestration. Each grain is a unique actor identified by a key, encapsulating domain logic and state.

## Core Concepts

### Grains - Stateful Actors

```
+---------+        +---------+        +---------+
| Grain A | <---> | Grain B | <---> | Grain C |
+---------+        +---------+        +---------+
    |                  |                  |
    +------ Orleans Cluster ------+
                  |
          PostgreSQL State Store
```

**Key Properties:**
- **Unique identifier** - GUID (IGrainWithGuidKey) or String (IGrainWithStringKey)
- **Stateful** - State persists to PostgreSQL via custom `State<T>` infrastructure
- **Location transparent** - Call any grain regardless of cluster node

### State Type

All grain state uses a single custom type: `State<T>`.

```csharp
// Inject in grain constructor
public MyGrain([State] State<MyState> state) { ... }

// State class — must implement IStateValue
[GenerateSerializer]
public class MyState : IStateValue {
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Value { get; set; } = string.Empty;
    public int Version => 0;
}
```

## State Management

### State Operations

```csharp
// Read + modify + write — returns updated value
var state = await _state.Update(s => {
    s.Value = newValue;
});

// Read + modify + write — returns void
await _state.Write(s => { s.Value = newValue; });

// Read only
var state = await _state.ReadValue();

// Read + transform
var value = await _state.Read(s => s.Value);
```

## Transactions

Custom `[Transaction]` attribute (from `Infrastructure` namespace, not Orleans native). Mark interface methods that participate in transactions:

```csharp
public interface IMyGrain : IGrainWithGuidKey {
    [Transaction]
    Task SetValue(int value);
}
```

Run multiple grain calls atomically:

```csharp
await orleans.InTransaction(async () => {
    await grainA.Initialize();
    await grainB.Initialize();  // both commit or both rollback
});
```

## Common Patterns

### 1. Handle Pattern - Convenient Facade

Access multiple related grains through single handle:

```csharp
// Usage
var handle = orleans.CreateUserHandle(userId);
await handle.Entity.SetName("Name");           // User grain
await handle.Progression.AddRecord(record);    // Progression grain
await handle.Deck.Create(deckData);            // Deck grain
```

### 2. Projection Pattern - Real-time Client Sync

Broadcast grain state changes to connected clients:

```csharp
// Backend: Cache and send to client
await projection.SendCached(state);
```

### 3. StateCollection - Persistent Collections

`StateCollection<TKey, TValue>` — loads all items from DB on startup, stays in sync via messaging:

```csharp
public class MyCollection(StateCollectionUtils<Guid, MyItemState> utils)
    : StateCollection<Guid, MyItemState>(utils), IMyCollection;

// Grain writes to collection
await _collection.OnUpdatedTransactional(state.Id, state);  // inside transaction
```

### 4. Service Discovery Pattern

Locate game servers and send requests via messaging:

```csharp
var server = _serviceDiscovery.RandomServer();
var pipeId = new MessagePipeServiceRequestId(server, typeof(MyRequest));
var response = await _messaging.SendPipe<MyResponse>(pipeId, request);
```

## State Registration

State info defined in `Common/Lookups/StatesLookup.cs`, registered in `ProjectsSetupExtensions.AddStates()`:

```csharp
// StatesLookup.cs
public static readonly Info MyEntity = new() {
    TableName = "state_my_entity",
    StateName = "my_entity",
    KeyType = GrainKeyType.Guid
};

// ProjectsSetupExtensions.cs — AddStates()
Add<MyState>(StatesLookup.MyEntity);
```

## Critical Rules

1. **`[State]` on constructor parameter** - Triggers `IStateFactory` to create `State<T>`
2. **State class implements `IStateValue`** - Required: `int Version => 0;`
3. **`[Transaction]` is custom** - From `Infrastructure` namespace, not Orleans native
4. **No `[Reentrant]`** - Not needed with custom transaction system
5. **Grain keys are persistent** - Same key always maps to same grain across restarts
6. **One grain instance per key** - Orleans ensures singleton per key per cluster
7. **Automatic rollback** - Failed transactions roll back in-memory state

## Key Files

| File | Purpose |
|------|---------|
| `Common/Lookups/StatesLookup.cs` | State table names, state names, key types |
| `Orchestration/Extensions/ProjectsSetupExtensions.cs` | Registers state types in `AddStates()` |
| `Infrastructure/Orleans/State/State.cs` | `State<T>` implementation |
| `Infrastructure/Orleans/State/StateExtensions.cs` | `Update()`, `Write()`, `ReadValue()`, `Read()` |
| `Infrastructure/Data/Collections/StateCollection.cs` | `StateCollection<TKey, TValue>` base |
| `Infrastructure/Orleans/Utils/OrleansUtils.cs` | `IOrleans` implementation |
| `Meta/Users/Entities/User.cs` | Grain + `State<T>` example |
| `Meta/Bots/BotCollection.cs` | `StateCollection` example |
