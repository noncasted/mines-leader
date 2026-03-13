# Orleans State (Resource Management)

Full details: → [docs/COMMON_ORLEANS.md](../docs/COMMON_ORLEANS.md)

## Two State Types — Choose Correctly

| Type | Use for | Example |
|------|---------|---------|
| `ITransactionalState<T>` | Entity data, frequently updated | User, Match |
| `IPersistentState<T>` | Dictionary collections | BotCollection, UsersCollection |

## Modifying State

```csharp
// ONLY way to write — returns updated snapshot
var snapshot = await _state.Update(s => {
    s.Name = name;
    s.UpdatedAt = DateTime.UtcNow;
});

// Reading without modification
var current = await _state.GetCurrentState();
```

## Adding New State — 4 Steps (ALL required)

**Step 1** — constant in `backend/Infrastructure/Orleans/Utils/States.cs`:
```csharp
public const string MyEntity = "my_entity";
```

**Step 2** — attribute class in same file:
```csharp
public class MyEntityAttribute() : TransactionalStateAttribute(MyEntity, MyEntity);
// or for persistent:
public class MyEntityAttribute() : PersistentStateAttribute(MyEntity, MyEntity);
```

**Step 3** — register in `StateAttributesExtensions`:
```csharp
AddTransactionalAttribute<States.MyEntityAttribute>();
// or:
AddPersistentAttribute<States.MyEntityAttribute>();
```

**Step 4** — add to `States.StateTables` list (creates DB table):
```csharp
public static readonly IReadOnlyList<string> StateTables = [
    // ... existing
    MyEntity,
];
```

## Which Type to Use
- Entity with CRUD operations → `ITransactionalState<T>`
- Collection/registry storing multiple items → `IPersistentState<T>` via `AddressableDictionary`
