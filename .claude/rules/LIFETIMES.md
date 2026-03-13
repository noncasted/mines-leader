# Lifetimes (Resource Management)

Full details: → [docs/COMMON_LIFETIMES.md](../docs/COMMON_LIFETIMES.md)

## The Rule
**EVERY `Advise()`/`View()`/`ListenClick()` MUST have non-null Lifetime. No Lifetime = memory leak.**

## Creating
```
new Lifetime()                  // standalone, call .Terminate() manually
parent.Child()            // auto-terminates when parent terminates
TerminatedLifetime.Instance     // pre-terminated (use for disabled features)
```

## Using
```
lifetime.IsTerminated
lifetime.Terminate()            // idempotent (safe to call twice)
lifetime.Listen(() => { })      // run on termination
lifetime.Intersect(other)       // terminates when either parent terminates
```

## Which Lifetime to Use
- **In `OnSetup(lifetime)`** → use the parameter directly
- **In collection `View`** → use `item.Lifetime` for item-level subscriptions
- **One-shot operation** → `new Lifetime()`, terminate when done
- **Service class** → inject via `[Inject] private IReadOnlyLifetime _lifetime`
