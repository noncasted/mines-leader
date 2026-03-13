# Reactive: Values & Properties

**TL;DR:** ViewableProperty = named state. Change value → all listeners notified. New listener → gets current value immediately.

## When to Use
✅ Current state (health, level, score)
✅ UI binding (listeners need initial value)
❌ One-time notifications (use EventSource)
❌ Collections (use ViewableList)

## LifetimedValue vs ViewableProperty

```csharp
// LifetimedValue - anonymous value container
var ltValue = new LifetimedValue<int>(100);

// ViewableProperty - named (same with methods)
var health = new ViewableProperty<int>(100);
health.Set(90);
health.Increase(); // For int
health.Decrease();
```

## View vs Advise

```csharp
var health = new ViewableProperty<int>(100);

// Advise = future only
health.Advise(lifetime, hp => { }); // Doesn't see 100

// View = immediate + future
health.View(lifetime, hp => { }); // Sees 100 immediately
```

## Boolean & Integer Extensions

```csharp
var isAlive = new ViewableProperty<bool>(true);
isAlive.AdviseTrue(lifetime, () => { });      // Only when true
await isAlive.WaitTrue(lifetime);              // Async wait

var gold = new ViewableProperty<int>(100);
gold.Increase();                               // +1
gold.Add(50);                                  // +50
gold.IsGreater(50);                            // Predicate
```

## Full Code Examples

Complete examples with all ViewableProperty scenarios:
→ [Assets/Docs/Claude/Docs_Reactive.cs](../../client/Assets/Docs/Claude/Docs_Reactive.cs)

Specific examples:
- [LifetimedValue](../../client/Assets/Docs/Claude/Docs_Reactive.cs#L54)
- [ViewableProperty](../../client/Assets/Docs/Claude/Docs_Reactive.cs#L75)
- [Boolean extensions](../../client/Assets/Docs/Claude/Docs_Reactive.cs#L75)
- [Integer extensions](../../client/Assets/Docs/Claude/Docs_Reactive.cs#L75)

## Key Rules

| Rule | Impact |
|------|--------|
| View = immediate + future | UI gets current instantly |
| Advise = future only | First call on change only |
| Value lifetime = value validity | Each value has own lifetime |
| Always use Lifetime | Memory leak if missing |

## Common Pattern: UI Binding

```csharp
public void OnSetup(IReadOnlyLifetime lifetime) {
    _character.Health.View(lifetime, health => {
        _healthText.text = $"HP: {health}";
    });
}
```

## Common Mistakes

❌ Using Advise for UI: `health.Advise(lt, hp => { })` (missing initial)
✅ Use View: `health.View(lt, hp => { })`

❌ Forgot lifetime: `health.View(null, hp => { })`
✅ Always pass: `health.View(lifetime, hp => { })`

## Quick Reference

**Creating:**
```
new ViewableProperty<T>(initial)
```

**Setting:**
```
property.Set(newValue)
property.Increase()
property.Decrease()
property.Add(amount)
```

**Subscribing:**
```
property.View(lifetime, handler)        // Immediate + future
property.Advise(lifetime, handler)      // Future only
property.AdviseTrue(lifetime, action)   // Boolean only
```

**Async:**
```
await property.WaitTrue(lifetime)
await property.WaitFalse(lifetime)
```

## Gotchas & Edge Cases

### View vs Advise Timing

```csharp
var hp = new ViewableProperty<int>(100);

// ✅ View fires callback immediately with current value
hp.View(lifetime, h => Debug.Log($"HP: {h}"));
// → Prints "HP: 100" immediately

// Advise fires NEXT time value changes
hp.Advise(lifetime, h => Debug.Log($"HP: {h}"));
// → Won't print now, only on next Set()
```

**Why:** View includes current state, Advise waits for change.

### Multiple View Subscriptions

```csharp
var health = new ViewableProperty<int>(100);

// Each View gets the current value
health.View(lt1, h => Debug.Log($"UI1: {h}"));  // Prints 100
health.View(lt2, h => Debug.Log($"UI2: {h}"));  // Prints 100

health.Set(90);
// Both print 90
```

**Why:** Each subscription is independent and gets the current value.

### Advise After Set

```csharp
var hp = new ViewableProperty<int>(100);
hp.Set(90);

// Advise registers after change - initial callback won't fire with 100
hp.Advise(lifetime, h => Debug.Log($"HP: {h}"));
// → Won't print 100, only future changes

// But View will see current 90
hp.View(lifetime, h => Debug.Log($"HP: {h}"));
// → Prints 90 immediately
```

**Why:** Advise only notifies on future changes, View includes current state.

### Boolean Property Extensions

```csharp
var isAlive = new ViewableProperty<bool>(true);

// AdviseTrue only fires when True
isAlive.AdviseTrue(lifetime, () => Debug.Log("Alive"));
// Fires immediately because current is true

// Setting to false won't trigger
isAlive.Set(false);  // AdviseTrue doesn't fire

// Setting back to true fires again
isAlive.Set(true);   // AdviseTrue fires again
```

**Why:** AdviseTrue/AdviseF filters based on boolean value.

### Integer Property Increase/Decrease

```csharp
var counter = new ViewableProperty<int>(5);

counter.Increase();  // Becomes 6, listeners fire
counter.Decrease();  // Becomes 5, listeners fire
counter.Add(10);     // Becomes 15, listeners fire

// Add with negative is allowed
counter.Add(-20);    // Becomes -5, listeners fire
```

**Why:** Increase/Decrease are sugar for Add(1) and Add(-1).

### View Callback Throws Exception

```csharp
var hp = new ViewableProperty<int>(100);

hp.View(lifetime, h => {
    throw new Exception("Error!");
});
// → Exception propagates to caller
// → Subscription is registered despite exception
```

**Why:** Callback exceptions don't prevent subscription registration.

## Next Steps
- **Events:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **Collections:** [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md)
- **Patterns:** [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md)
