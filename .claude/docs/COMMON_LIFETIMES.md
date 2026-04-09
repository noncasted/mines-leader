# Lifetimes: Resource Management Fundamentals

**TL;DR:** Lifetime = scope for subscriptions. When terminates → all children auto-terminate. **Rule: Every `Advise()`/`View()` MUST have Lifetime parameter or memory leak.**

## When to Use
✅ All subscriptions (EventSource, ViewableProperty, ViewableList)
✅ Resource cleanup, parent-child relationships
❌ Static global data

## Core Concept
```
Parent Lifetime
├─ Child 1 (auto-terminates with parent)
└─ Child 2 (auto-terminates with parent)
```

## Creating Lifetimes

```csharp
// Standalone (manual cleanup)
var lifetime = new Lifetime();
lifetime.Terminate();

// Child (auto-cleanup with parent)
var child = parent.Child();

// From CancellationToken
var lifetime = token.ToLifetime();
```

## Key Rules

| Rule | Impact |
|------|--------|
| All Advise/View need Lifetime | Missing = memory leak |
| Child auto-terminates with parent | Automatic cleanup |
| Terminate() twice is safe | Idempotent |
| Listeners cleared after termination | No callbacks after death |

## Common Pattern: Scoped Subscription

```csharp
public void OnSetup(IReadOnlyLifetime lifetime) {
    _button.ListenClick(lifetime, OnButtonClicked);
    // Auto-cleaned when lifetime terminates
}
```

## Full Code Examples

Complete runnable examples with all scenarios:
→ [Assets/Common/Docs/Claude/Docs_Lifetimes.cs](../../client/Assets/Common/Docs/Claude/Docs_Lifetimes.cs)

Specific examples:
- [Standalone lifetime](../../client/Assets/Common/Docs/Claude/Docs_Lifetimes.cs#L16)
- [Child lifetime](../../client/Assets/Common/Docs/Claude/Docs_Lifetimes.cs#L31)
- [Lifetime hierarchy](../../client/Assets/Common/Docs/Claude/Docs_Lifetimes.cs#L49)
- [Scoped subscriptions](../../client/Assets/Common/Docs/Claude/Docs_Lifetimes.cs#L118)

## Common Mistakes

❌ Forgot lifetime: `source.Advise(null, OnEvent)`
✅ Always pass: `source.Advise(lifetime, OnEvent)`

## Quick Reference

**Creating:**
```
new Lifetime()
parentLifetime.Child()
token.ToLifetime()
```

**Using:**
```
lifetime.IsTerminated
lifetime.Terminate()
lifetime.Listen(() => { })
```

**Subscriptions:**
```
eventSource.Advise(lifetime, handler)
property.View(lifetime, handler)
```

## Gotchas & Edge Cases

### TerminatedLifetime

```csharp
var lt = new Lifetime();
lt.Terminate();

// ⚠️ Callbacks registered after termination fire IMMEDIATELY
lt.Advise(lt, () => Debug.Log("Fires now!"));
// → Prints "Fires now!" immediately, not queued
```

**Why:** Cleanup already happened, so listener is invoked synchronously.

### Double Terminate

```csharp
var lt = new Lifetime();
lt.Terminate();
lt.Terminate();  // ✅ Safe - idempotent operation
```

**Why:** Terminate is idempotent - second call is no-op.

### Timing: Callbacks After Terminate

```csharp
var lt = new Lifetime();
var sub = lt.Advise(lt, () => Debug.Log("Event"));
lt.Terminate();

sub.Invoke();  // ❌ Callback does NOT fire
// Subscriptions already cleaned, Invoke has no effect
```

**Why:** All listeners are removed during Terminate.

### Child Lifetime After Parent Terminate

```csharp
var parent = new Lifetime();
var child = parent.Child();

parent.Terminate();
// child is also terminated (auto-cleanup)

child.Advise(child, OnEvent);  // ❌ Fires immediately (child terminated)
```

**Why:** Child auto-terminates with parent.

### Circular Reference Risk

```csharp
// ❌ WRONG - lifetime captures this, this has lifetime
public class Service {
    private Lifetime _lifetime;

    public void Setup() {
        _lifetime.Advise(_lifetime, () => {
            // this keeps _lifetime alive
        });
    }
    // GC can't reclaim Service because _lifetime has reference to callback
}

// ✅ CORRECT - use WeakReference if needed
public class Service {
    private readonly WeakReference<Lifetime> _lifetime;
    // ...
}
```

**Why:** Callbacks captured by lifetime can prevent garbage collection.

## Next Steps
- **Patterns:** [COMMON_LIFETIMES_PATTERNS.md](COMMON_LIFETIMES_PATTERNS.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **DI:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
