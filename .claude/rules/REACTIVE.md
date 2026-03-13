# Reactive System (Events & State)

Full examples: → [docs/COMMON_REACTIVE_*.md](../docs/COMMON_REACTIVE_BASICS.md)

## Three Types — Choose Correctly

| Type | Use for | Wrong use |
|------|---------|-----------|
| `EventSource<T>` | One-time signals, notifications | Storing state |
| `ViewableProperty<T>` | Current state, UI bindings | Events without state |
| `ViewableList<T>` | Dynamic collections (inventory, teams) | Static lists |

## View vs Advise — CRITICAL

- **`View(lifetime, callback)`** — fires immediately with current value + future changes → **use for ALL UI bindings**
- **`Advise(lifetime, callback)`** — future changes only → use for event notifications

Wrong: `_health.Advise(lt, hp => healthText.text = ...)` — UI shows nothing on load.
Correct: `_health.View(lt, hp => healthText.text = ...)`

## Item Subscription Lifetime — CRITICAL

Wrong (memory leak when item removed):
```csharp
items.View(sceneLifetime, item => item.Events.Advise(sceneLifetime, OnEvent));
```

Correct (auto-cleanup):
```csharp
items.View(sceneLifetime, item => item.Events.Advise(item.Lifetime, OnEvent));
```

## Quick API
```
EventSource:       _event.Invoke(val)   |  _event.Advise(lt, handler)
ViewableProperty:  _prop.Set(val)       |  _prop.View(lt, handler)
ViewableList:      _list.Add(item)      |  _list.View(lt, item => ...)
                   _list.Remove(item)   |  // Remove terminates item.Lifetime
```
