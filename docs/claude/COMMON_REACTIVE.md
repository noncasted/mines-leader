# Common: Reactive

Reactive data structures with lifetime-scoped subscriptions and automatic cleanup.

## Architecture

```
                    IEventSource<T>
                          |
          +---------------+----------------+
          |               |                |
    EventSource     ILifetimedValue   IViewableList
          |               |                |
   ViewableDelegate  ViewableProperty  ViewableDictionary
```

**Key Principle:** Every subscription is scoped to a Lifetime. When lifetime terminates - subscription removed automatically.

## Core Types

### EventSource (fire-and-forget events)

```csharp
public interface IEventSource<T> : IDisposable {
    void Advise(IReadOnlyLifetime lifetime, Action<T> handler);
}

// Usage
var onDamage = new EventSource<int>();
onDamage.Advise(lifetime, damage => Console.WriteLine($"Hit: {damage}"));
onDamage.Invoke(50);  // fires handler
```

**Variants:**
- `EventSource` - no parameters
- `EventSource<T>` - one parameter
- `EventSource<T1, T2>` - two parameters
- `EventSource<T1, T2, T3>` - three parameters

### ViewableDelegate (typed event wrapper)

Same as EventSource, but with explicit interface for public exposure.

```csharp
public interface IViewableDelegate<T> : IEventSource<T> { }

// Expose readonly interface
public IViewableDelegate<int> OnHealthChanged => _onHealthChanged;
private readonly ViewableDelegate<int> _onHealthChanged = new();
```

### LifetimedValue (value + value lifetime)

Value with its own lifetime that terminates when value changes.

```csharp
public interface ILifetimedValue<T> : IEventSource<IReadOnlyLifetime, T> {
    T Value { get; }
    IReadOnlyLifetime ValueLifetime { get; }
}
```

**Key feature:** Each value has its own lifetime. When `Set()` is called:
1. Old value's lifetime terminates
2. New lifetime created
3. Listeners notified with (newLifetime, newValue)

```csharp
var property = new LifetimedValue<string>("initial");

property.Advise(lifetime, (valueLifetime, value) => {
    // valueLifetime - valid until next Set()
    // Use for value-scoped subscriptions
});

property.Set("new value");  // old valueLifetime terminated
```

### ViewableProperty (simplified LifetimedValue)

Same as LifetimedValue, named for clarity.

```csharp
var health = new ViewableProperty<int>(100);

// View = Advise + immediate callback with current value
health.View(lifetime, value => UpdateUI(value));

// Just advise (no immediate callback)
health.Advise(lifetime, (_, value) => OnChanged(value));
```

**Extensions:**
```csharp
// For ViewableProperty<int>
health.Increase();     // +1
health.Decrease();     // -1
health.Add(10);        // +10
health.Remove(5);      // -5
health.IsZero();       // check == 0

// For ViewableProperty<bool>
isReady.WaitTrue(lifetime);   // async wait
isReady.WaitFalse(lifetime);
isReady.AdviseTrue(lifetime, () => { });  // only on true
```

### ViewableList (observable collection)

```csharp
public interface IViewableList<T> : IEventSource<IReadOnlyLifetime, T>, IReadOnlyList<T> {
    IReadOnlyLifetime GetLifetime(T value);
}
```

**Key feature:** Each item has its own lifetime. Remove item = terminate its lifetime.

```csharp
var players = new ViewableList<Player>();

// Subscribe to new additions
players.Advise(lifetime, (itemLifetime, player) => {
    // itemLifetime valid until player removed
    player.OnDeath.Advise(itemLifetime, () => { });
});

// View = Advise + iterate existing items
players.View(lifetime, (itemLifetime, player) => SetupUI(player));

var itemLifetime = players.Add(newPlayer);  // returns item's lifetime
players.Remove(player);  // item's lifetime terminated
```

### ViewableDictionary (observable key-value)

```csharp
public interface IViewableDictionary<TKey, TValue> :
    IEventSource<IReadOnlyLifetime, TKey, TValue>,
    IReadOnlyDictionary<TKey, TValue>
{
    IReadOnlyLifetime GetLifetime(TKey key);
}
```

```csharp
var sessions = new ViewableDictionary<string, Session>();

sessions.View(lifetime, (itemLifetime, key, session) => {
    // itemLifetime valid until key removed
});

var itemLifetime = sessions.Add("user123", session);
sessions.Remove("user123");  // lifetime terminated
```

**Lifetimed Add:**
```csharp
// Auto-remove when scope lifetime terminates
sessions.AddLifetimed(scopeLifetime, "key", value);
```

## ModifiableList (iteration-safe list)

Internal helper. Safe to Add/Remove during iteration.

```csharp
var list = new ModifiableList<Action>();

foreach (var action in list) {
    action();  // safe to add/remove here
}
// Changes applied after iteration completes
```

## Advise vs View

| Method | Behavior |
|--------|----------|
| `Advise` | Only future events |
| `View` | Advise + immediate callback with current state |

```csharp
// Advise: only future changes
property.Advise(lifetime, (_, value) => { });

// View: immediate + future changes
property.View(lifetime, value => UpdateUI(value));
```

## Async Waiting

```csharp
// Wait for event to fire
await viewableDelegate.WaitInvoke(lifetime);
await viewableDelegate.WaitInvoke<T>(lifetime);  // returns value

// Wait for condition
await boolProperty.WaitTrue(lifetime);
await boolProperty.WaitFalse(lifetime);
```

## Rules

1. **All subscriptions require Lifetime** - no memory leaks

2. **View = Advise + immediate invocation** - convenient for UI binding

3. **Item lifetime = item existence** - remove item = terminate lifetime

4. **Value lifetime = value validity** - Set() = terminate old, create new

5. **Dispose clears all** - subscriptions and lifetimes

## Typical Patterns

### UI Binding

```csharp
healthProperty.View(lifetime, value => healthBar.SetValue(value));
```

### Item-scoped Subscription

```csharp
enemies.View(lifetime, (enemyLifetime, enemy) => {
    enemy.OnAttack.Advise(enemyLifetime, ProcessAttack);
    // Auto-unsubscribed when enemy removed
});
```

### Conditional Logic

```csharp
isConnected.View(lifetime, connected => {
    if (connected) ShowOnline();
    else ShowOffline();
});
```

### Async Coordination

```csharp
await isReady.WaitTrue(lifetime);
await onComplete.WaitInvoke(lifetime);
```

## Key Files

| File | Purpose |
|------|---------|
| `Events/Abstract/IEventSource.cs` | Base event interface |
| `Events/EventSource.cs` | Event implementation |
| `Events/Abstract/ILifetimedValue.cs` | Value + lifetime interface |
| `Events/LifetimedValue.cs` | Value + lifetime impl |
| `Events/LifetimedValueExtensions.cs` | View, WaitTrue/False |
| `DataTypes/Properties/ViewableProperty.cs` | Named LifetimedValue |
| `DataTypes/Lists/ViewableList.cs` | Observable list |
| `DataTypes/Dictionaries/ViewableDictionary.cs` | Observable dictionary |
| `DataTypes/Delegates/ViewableDelegate.cs` | Typed event wrapper |
| `DataTypes/Lists/ModifiableList.cs` | Iteration-safe list |
