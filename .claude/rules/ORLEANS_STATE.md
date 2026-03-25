# Orleans State

Full details: → [docs/COMMON_ORLEANS.md](../docs/COMMON_ORLEANS.md)

## One State Type: `State<T>`

All grain state uses `State<T>` — both entity data and collection entries.

```csharp
// Inject in grain constructor
public MyGrain([State] State<MyState> state) {
    _state = state;
}
private readonly State<MyState> _state;
```

## State Class Requirements

```csharp
[GenerateSerializer]
public class MyState : IStateValue {          // must implement IStateValue
    [Id(0)] public Guid Id { get; set; }      // Id(N) sequential, no gaps
    [Id(1)] public string Name { get; set; } = string.Empty;
    public int Version => 0;                   // always required
}
```

## Operations

```csharp
// Read + modify + write — returns updated value
var state = await _state.Update(s => {
    s.Name = name;
});

// Read + modify + write — returns void
await _state.Write(s => {
    s.Name = name;
});

// Read only — returns T
var state = await _state.ReadValue();

// Read + transform — returns result
var name = await _state.Read(s => s.Name);
```

## Adding New State — 3 Steps (ALL required)

**Step 1** — add entry to `backend/Common/Lookups/StatesLookup.cs`:
```csharp
public static readonly Info MyEntity = new() {
    TableName = "state_my_entity",
    StateName = "my_entity",
    KeyType = GrainKeyType.Guid       // matches grain key type
};

// Also add to All list at the bottom of StatesLookup
```

**Step 2** — register in `ProjectsSetupExtensions.AddStates()`:
```csharp
Add<MyState>(StatesLookup.MyEntity);
```

**Step 3 (collections only)** — register `StateCollection` in service extension:
```csharp
builder.AddStateCollection<MyCollection, Guid, MyState>()
    .As<IMyCollection>();
```

## Collections: StateCollection<TKey, TValue>

Replaces `AddressableDictionary`. In-memory dictionary that auto-loads from DB on startup and stays in sync via messaging.

```csharp
// Interface
public interface IMyCollection : IStateCollection<Guid, MyState> { }

// Implementation — minimal
public class MyCollection(StateCollectionUtils<Guid, MyState> utils)
    : StateCollection<Guid, MyState>(utils), IMyCollection;

// Usage from grain
await _collection.OnUpdated(state.Id, state);             // direct + push
await _collection.OnUpdatedTransactional(state.Id, state); // push within transaction

// Usage from service (read-only)
var item = _collection[id];
var all = _collection.Values;
_collection.Updated.Advise(lifetime, OnChange);
```

## Which Approach to Use
- Single entity grain → `[State] State<T>` in constructor
- Collection/registry → `StateCollection<TKey, TValue>` + grain calls `OnUpdatedTransactional`
