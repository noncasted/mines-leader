# Orleans: Full Reference

Quick rules: → [rules/ORLEANS_GRAINS.md](../rules/ORLEANS_GRAINS.md) | [rules/ORLEANS_STATE.md](../rules/ORLEANS_STATE.md)

## Quick Navigation

| Looking for... | Go to section |
|----------------|---------------|
| Grain interface + class boilerplate | Grain Pattern |
| ITransactionalState vs IPersistentState | State Types |
| GetGrain, cross-grain calls | IOrleans Interface |
| Multiple items stored as a collection | AddressableDictionary |
| Read-only projection of a collection | AddressableDictionaryView |
| Push updates to clients | Messaging |
| OnActivateAsync, OnDeactivateAsync | Grain Lifecycle |

---

## Grain Pattern

```csharp
// Interface — one key type only
public interface IMyGrain : IGrainWithGuidKey {
    [Transaction]  // only if called inside a transaction
    Task<string> GetValue();

    Task FireAndForget();  // no attribute if not transactional
}

// Implementation
[Reentrant]  // always — allows concurrent reentrant calls
public class MyGrain : Grain, IMyGrain {
    // Constructor injection — never [Inject] fields
    public MyGrain(
        [States.MyState] ITransactionalState<MyState> state,
        IOrleans orleans,
        ILogger<MyGrain> logger) {
        _state = state;
        _orleans = orleans;
        _logger = logger;
    }

    private readonly ITransactionalState<MyState> _state;
    private readonly IOrleans _orleans;
    private readonly ILogger<MyGrain> _logger;

    public async Task<string> GetValue() {
        var s = await _state.GetCurrentState();
        return s.Value;
    }
}
```

**Key types:**
- `IGrainWithGuidKey` — Guid-based identity (most common for aggregates)
- `IGrainWithStringKey` — string-based identity (named singleton-like services)
- `CommonGrain` — base class alternative to `Grain`, adds `StringId`, `Grains`, `Reference` shortcuts

---

## State Types

### ITransactionalState — entity data

```csharp
// Read
var state = await _state.GetCurrentState();

// Write — returns updated snapshot
var snapshot = await _state.Update(s => {
    s.Name = name;
});

// State class requirements
[GenerateSerializer]
[Alias(States.MyEntity)]      // must match States constant
public class MyState {
    [Id(0)] public Guid Id { get; set; }    // Id(N) sequential, no gaps
    [Id(1)] public string Name { get; set; } = string.Empty;
}
```

### IPersistentState — collection data

Used as the backing store for `AddressableDictionary` subclasses. Do not use directly for entity data.

---

## IOrleans Interface

Central access point to Orleans from non-grain code (gateways, services):

```csharp
public interface IOrleans {
    IClusterClient Client { get; }
    IGrainFactory Grains { get; }
    IDbSource DbSource { get; }
    ILogger Logger { get; }
}

// Extension methods (use these, not Grains directly):
orleans.GetGrain<IMyGrain>(guid);          // IGrainWithGuidKey
orleans.GetGrain<IMyGrain>("key");         // IGrainWithStringKey
orleans.GetGrain<IMyGrain>();              // IGrainWithGuidKey with Guid.Empty (singletons)
orleans.GetGrains<IMyGrain>(listOfGuids); // batch lookup

// Run code inside a transaction
await orleans.InTransaction(async () => {
    await grainA.DoSomething();
    await grainB.DoSomething();  // both in same transaction
});
```

**Inside a grain** — use `GrainFactory` directly (injected as `IGrainFactory`), or inherit `CommonGrain` for `.Grains` shortcut.

---

## AddressableDictionary

Use when you need a persistent collection of items addressable by key, with client sync via messaging.

### How to Create

```csharp
// 1. State class
[GenerateSerializer]
public class MyCollectionState : AddressableDictionaryState<Guid, MyItem> { }

// 2. Interface
public interface IMyCollection : IAddressableDictionary<Guid, MyItem> {
    [Transaction]
    Task AddOrUpdate(MyItem item);

    [Transaction]
    Task Remove(Guid id);
}

// 3. Grain implementation
public class MyCollection : AddressableDictionary<MyCollectionState, Guid, MyItem>, IMyCollection {
    public MyCollection(
        [States.MyCollection] IPersistentState<MyCollectionState> state,
        IMessaging messaging) : base(state, messaging) { }

    public Task AddOrUpdate(MyItem item) => Write(item.Id, item);
    public Task Remove(Guid id) => Erase(id);
}

// 4. Item class
[GenerateSerializer]
public class MyItem {
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
}
```

`Write()` and `Erase()` are provided by `AddressableDictionary<>` base class.

---

## AddressableDictionaryView

Read-only projection of an `AddressableDictionary` in backend services. Loads full state on startup, then stays in sync via messaging.

```csharp
// Interface
public interface IMyCollectionView : IAddressableDictionaryView<Guid, MyItem> { }

// Implementation — minimal, base class does everything
public class MyCollectionView
    : AddressableDictionaryView<Guid, MyItem, IMyCollection>, IMyCollectionView {
    public MyCollectionView(IOrleans orleans, IMessaging messaging)
        : base(orleans, messaging) { }
}

// Usage (read access)
var item = _view[itemId];                  // dictionary access
var all = _view.Values;                    // all items
_view.Updated.Advise(lifetime, OnChange); // subscribe to changes
```

### Registration

```csharp
// In service extension method:
builder.AddAddressableDictionaryView<IMyCollectionView, MyCollectionView>();
// This registers it as singleton AND wires up initialization (AsSetupLoopStage)
```

**Note:** `IAddressableDictionaryView` implements `IReadOnlyDictionary`, so it can be used directly as a dictionary.

---

## Messaging

Used to push state updates to clients (or between backend services) when they subscribe to a queue.

```csharp
// Push update to all subscribers of a queue
await _messaging.PushTransactionalQueue(queueId, new MyUpdateMessage { ... });

// Subscribe to a queue (backend service or gateway)
await _messaging.ListenQueue<MyUpdateMessage>(lifetime, queueId, OnUpdate);

private void OnUpdate(MyUpdateMessage message) {
    // handle incoming update
}
```

**Lifetime** is used here — it exists in backend too, not only in client. Subscription lives as long as the lifetime is active.

### Message class requirements

```csharp
[GenerateSerializer]
public class MyUpdateMessage {
    [Id(0)] public Dictionary<Guid, MyItem> Updates { get; set; } = new();
    [Id(1)] public List<Guid> Removals { get; set; } = new();
}
```

**AddressableDictionary uses messaging internally** — `AddressableDictionaryView` listens to the queue automatically. You only write messaging directly if building a custom sync mechanism.

---

## Grain Lifecycle

```csharp
// Called when grain is activated (first call after idle)
public override Task OnActivateAsync(CancellationToken cancellationToken) {
    _task.Delay = Options.Delay;
    return base.OnActivateAsync(cancellationToken);
}

// Called before grain is deactivated
public override async Task OnDeactivateAsync(
    DeactivationReason reason,
    CancellationToken cancellationToken) {
    // throwing prevents deactivation (keeps grain alive):
    if (DateTime.UtcNow - _lastActivity < TimeSpan.FromMinutes(3))
        throw new Exception("Keeping grain alive");

    await base.OnDeactivateAsync(reason, cancellationToken);
}
```

Use `OnActivateAsync` for: starting timers, loading supplementary data not in state.
Use `OnDeactivateAsync` for: flushing pending writes, preventing premature deactivation.

---

## Key Files

| File | Purpose |
|------|---------|
| `backend/Infrastructure/Orleans/Utils/States.cs` | All state constants, attribute classes, StateTables list |
| `backend/Infrastructure/Orleans/Utils/StateAttributesExtensions.cs` | Registers state attributes with DI |
| `backend/Infrastructure/Orleans/Utils/OrleansUtils.cs` | IOrleans implementation + extension methods |
| `backend/Infrastructure/Orleans/Utils/CommonGrain.cs` | Base grain class with shortcuts |
| `backend/Infrastructure/Data/Collections/AddressableDictionary.cs` | AddressableDictionary base |
| `backend/Infrastructure/Data/Collections/AddressableDictionaryView.cs` | AddressableDictionaryView base |
| `backend/Meta/Bots/BotCollection.cs` | AddressableDictionary example |
| `backend/Meta/Users/Entities/User.cs` | ITransactionalState example |
| `backend/Meta/Matches/Match.cs` | Multi-grain transaction example |
