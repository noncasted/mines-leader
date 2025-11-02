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
- **Stateful** - State persists to PostgreSQL via Orleans
- **Location transparent** - Call any grain regardless of cluster node
- **Reentrant** - Always use `[Reentrant]` to prevent deadlocks

### State Types

```csharp
// Transactional State - ACID guarantees
ITransactionalState<T>

// Regular State - Simple persistence (rarely used)
IPersistentState<T>
```

**Rule:** Check grain definition to see which state type it uses. Almost all grains use `ITransactionalState<T>`.

### State Storage

All state names and table mappings defined in `Common/Extensions/States.cs`:

```csharp
// Example state definition
[Alias(States.User_Entity)]
public class UserState { ... }
```

Table mappings:
- `States.User_Entity` → PostgreSQL table `user_entity`
- `States.Match_Entity` → PostgreSQL table `match_entity`
- `States.Messaging_Queue` → PostgreSQL table `messaging_queue`

## State Management

### ITransactionalState Pattern

```csharp
[Reentrant]
public class MyGrain : Grain, IMyGrain
{
    private ITransactionalState<MyState> _state = null!;

    // Read-only operations
    public Task<int> GetValue() =>
        _state.PerformRead(state => state.Value);

    // Modify operations
    [Transaction(TransactionOption.Join)]
    public async Task SetValue(int value)
    {
        var newState = await _state.Update(state => {
            state.Value = value;
        });
        // Do something with updated state
    }
}
```

### Read Operations

```csharp
// Non-transactional read
return await _state.PerformRead(state => state.SomeField);

// Used for: Getting current values without modifications
```

### Update Operations

```csharp
// Transactional update
var newState = await _state.Update(state => {
    state.Field1 = newValue1;
    state.Field2 = newValue2;
    // Changes auto-persisted to PostgreSQL
});

// Returns: Updated state object
// Guarantees: ACID - all or nothing
```

**Critical:** Always mark update methods with `[Transaction()]` attribute.

### Batch Operations

```csharp
// Combine multiple updates (BatchWriter pattern)
public class MyBatcher : BatchWriter<MyState, MyEntry>
{
    protected override async Task Process(IReadOnlyList<MyEntry> entries)
    {
        // Process accumulated entries
    }
}

// Write transactional (only committed on transaction success)
await batcher.WriteTransactional(entry);

// Write immediate
await batcher.WriteDirect(entry);
```

## Grain Lifecycle

```
1. GetGrain<T>(key)
   |
2. OnActivate() - Grain activated on cluster node
   |
3. Method calls process requests
   |
4. State updates persisted to PostgreSQL
   |
5. OnDeactivate() - Grain idle, removed from memory
   |
6. State remains in PostgreSQL for next activation
```

### Key Attributes

```csharp
// Allow concurrent calls (required for all grains)
[Reentrant]
public class MyGrain : Grain { }

// Participate in Orleans transaction
[Transaction(TransactionOption.Join)]
public async Task DoSomething() { }

// Start new transaction (rare, for factory methods)
[Transaction(TransactionOption.Create)]
public async Task CreateSomething() { }
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

// Implementation
public class UserHandle
{
    public IUser Entity => _orleans.GetGrain<IUser>(_id);
    public IUserProgression Progression => _orleans.GetGrain<IUserProgression>(_id);
    public IUserDeck Deck => _orleans.GetGrain<IUserDeck>(_id);
}

// All use same ID internally
```

### 2. Projection Pattern - Real-time Client Sync

Broadcast grain state changes to connected clients:

```csharp
// Backend: Cache and send to client
await projection.SendCached(state);    // Send immediately + cache for new connections
await projection.Cache(state);         // Cache only, don't broadcast
await projection.SendOneTime(state);   // Send once, don't cache

// Protocol: Via messaging pipes to client
```

**Used By:**
- `User` grain sends name changes
- `UserProgression` sends XP updates
- `UserMatchHistory` sends new match entries
- All via `UserProjection` grain

### 3. Transactional Pattern - Atomic Multi-Grain Updates

```csharp
// Atomic across multiple grains
await orleans.InTransaction(async () => {
    var user = _orleans.GetGrain<IUser>(userId);
    var deck = _orleans.GetGrain<IUserDeck>(userId);

    await user.Initialize();    // All updates...
    await deck.Initialize();    // ...commit together
});

// Result: Both succeed or both fail (no partial updates)
```

**Used By:**
- User creation (User + Deck initialize together)
- Match setup (participants consistency)
- Progression updates (atomic state changes)

### 4. Service Discovery Pattern

Locate game servers and send requests via messaging:

```csharp
// Find target service
var server = _serviceDiscovery.RandomServer();

// Request-response via messaging pipe
var pipeId = new MessagePipeServiceRequestId(server, typeof(MyRequest));
var response = await _messaging.SendPipe<MyResponse>(pipeId, request);
```

**Used By:**
- MatchFactory finds game server for match
- LobbyFactory finds game server for lobby

## State Operations Reference

```csharp
// Read without modify
await _state.PerformRead(state => state.Value)

// Modify and return
var newState = await _state.Update(state => {
    state.Value = newValue;
});

// Clear state
await _state.ClearAsync()

// Write directly
await _state.WriteStateAsync()

// Read directly
await _state.ReadStateAsync()
```

## Critical Rules

1. **Always use [Reentrant]** - Prevents Orleans deadlocks from concurrent grain calls
2. **Mark update methods with [Transaction()]** - Ensures ACID guarantees
3. **State attributes matter** - `[States.UserEntity]` affects table mapping and serialization
4. **ITransactionalState for everything** - Don't use IPersistentState
5. **Grain keys are persistent** - Same key always maps to same grain across restarts
6. **One grain instance per key** - Orleans ensures singleton per key per cluster
7. **Automatic rollback** - Failed transactions auto-rollback, no manual cleanup needed
8. **Projection updates critical** - Missing ForceNotify = client sees stale state

## Logging Tags

| Tag | Components | Usage |
|-----|-----------|-------|
| `[User]` | User, UserProgression, UserDeck | User domain operations |
| `[Match]` | Match, Matchmaking | Match domain operations |
| `[Projection]` | UserProjection | State sync to clients |
| `[Messaging]` | MessageQueue, MessagePipe | Inter-service communication |
| `[BatchWriter]` | BatchWriter grains | Batch processing |

## Key Files

| File | Purpose |
|------|---------|
| `Common/Extensions/States.cs` | State name and table mappings |
| `Infrastructure/Orleans/Silo/Program.cs` | Orleans cluster bootstrap |
| `Backend/Users/Entities/Grains/User.cs` | Example: User grain |
| `Backend/Users/Projections/Grains/UserProjection.cs` | Example: Projection grain |
| `Backend/Matches/Entities/Grains/Match.cs` | Example: Match grain |
| `Infrastructure/StorableActions/Batchers/Grains/BatchWriter.cs` | Batch processing base |

## Integration Points

- **Messaging** - Pipes for request-response, Queues for broadcasts (see INFRASTRUCTURE_MESSAGING.md)
- **StorableActions** - BatchWriter for accumulated processing, ClusterState for shared configuration
- **PostgreSQL** - All state persisted via Orleans storage
- **Common** - Transaction helpers, Orleans extensions, state definitions