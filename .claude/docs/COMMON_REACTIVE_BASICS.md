# Reactive: Events & EventSource

**TL;DR:** EventSource = fire-and-forget event. Publisher invokes → listeners notified. Always with Lifetime.

## When to Use
✅ Broadcasting notifications (clicked, changed, completed)
✅ One-way signals
❌ Storing state (use ViewableProperty)
❌ Collections (use ViewableList)

## EventSource Variants

```csharp
// No params
new EventSource()
_onGameStarted.Invoke()
_onGameStarted.Advise(lifetime, () => { })

// With param
new EventSource<int>()
_onHealthChanged.Invoke(100)
_onHealthChanged.Advise(lifetime, health => { })

// Multiple params
new EventSource<string, float>()
_onCurrencyChanged.Invoke("gold", 100f)
```

## ViewableDelegate (Named Event Wrapper)

```csharp
private readonly ViewableDelegate<string> _onChat = new();
public IViewableDelegate<string> OnChat => _onChat;

_onChat.Invoke("hello");
_onChat.Advise(lifetime, msg => { });
```

## Key Rules

| Rule | Impact |
|------|--------|
| All Advise need Lifetime | Memory leak if missing |
| Fire = immediate callback | Synchronous notification |
| Register after fire = miss event | Event fires once |
| Expose as read-only | Client can't invoke |

## Full Code Examples

Complete examples with all EventSource variants:
→ [Assets/Common/Docs/Claude/Docs_Reactive.cs](../../client/Assets/Common/Docs/Claude/Docs_Reactive.cs)

Specific examples:
- [EventSource basic](../../client/Assets/Common/Docs/Claude/Docs_Reactive.cs#L24)
- [EventSource with parameters](../../client/Assets/Common/Docs/Claude/Docs_Reactive.cs#L39)
- [ViewableDelegate](../../client/Assets/Common/Docs/Claude/Docs_Reactive.cs#L39)

## Common Pattern: Publisher Service

```csharp
public class DamageSystem : MonoBehaviour, ISceneService, IScopeSetup {
    private readonly EventSource<int, int> _onDamage = new();
    public IEventSource<int, int> OnDamage => _onDamage;

    public void OnSetup(IReadOnlyLifetime lifetime) { }

    public void DealDamage(int targetId, int amount) {
        _onDamage.Invoke(targetId, amount);
    }
}
```

## Common Mistakes

❌ Forgot lifetime: `eventSource.Advise(null, OnEvent)`
✅ Always pass: `eventSource.Advise(lifetime, OnEvent)`

❌ Storing value in EventSource (latecomers miss it)
✅ Use ViewableProperty for state

## Quick Reference

**Creating:**
```
new EventSource()
new EventSource<T>()
new EventSource<T1, T2>()
new ViewableDelegate<T>()
```

**Using:**
```
eventSource.Invoke(value)
eventSource.Advise(lifetime, handler)
public IEventSource OnEvent => _onEvent;
```

## Next Steps
- **Values:** [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md)
- **Collections:** [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md)
- **Patterns:** [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md)
