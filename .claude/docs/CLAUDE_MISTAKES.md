# AI Self-Learning Log

**Historical record of mistakes and lessons. Always read before implementing MonoBehaviour services.**

## Lesson 1: MonoBehaviour Service Registration (CRITICAL)

### Mistake Made
```csharp
// WRONG - missing critical interfaces and Create() method
public class MySelector : MonoBehaviour {
    public void OnSetup(IReadOnlyLifetime lifetime) { }
    // Missing: ISceneService, IScopeSetup, Create()
    // Result: OnSetup() never runs!
}
```

### Correct Pattern
```csharp
// CORRECT - all pieces required
public class MySelector : MonoBehaviour, ISceneService, IScopeSetup {
    public void Create(IScopeBuilder builder) {
        builder.RegisterComponent(this).As<IScopeSetup>();
    }

    public void OnSetup(IReadOnlyLifetime lifetime) {
        // Now called when scope initializes
    }
}
```

→ [rules/MONOBEHAVIOUR.md](../rules/MONOBEHAVIOUR.md)

---

## Lesson 2: Item Subscriptions Must Use Item Lifetime

❌ WRONG - memory leak when item removed:
```csharp
items.View(sceneLifetime, item => {
    item.Events.Advise(sceneLifetime, OnEvent);
});
```

✅ CORRECT - auto-cleanup:
```csharp
items.View(sceneLifetime, item => {
    item.Events.Advise(item.Lifetime, OnEvent);
});
```

**Rule:** Always use `item.Lifetime` for item subscriptions.

---

## Lesson 3: View vs Advise for UI

❌ WRONG - UI shows nothing initially:
```csharp
_health.Advise(uiLifetime, hp => {
    healthText.text = $"HP: {hp}";
});
```

✅ CORRECT - UI shows current + future:
```csharp
_health.View(uiLifetime, hp => {
    healthText.text = $"HP: {hp}";
});
```

**Rule:** Always use `View()` for UI binding, `Advise()` for events only.

---

## Lesson 4: Dialogue System Frame Synchronization

❌ WRONG - Dialogue text not updating when frame changes:
```csharp
public void Show(DialogueTextTrack track, IReadOnlyLifetime lifetime) {
    SetSchemeFrames(track);  // Shows only once
    // No subscription to frame changes!
}
```

✅ CORRECT - Re-subscribe when frame changes:
```csharp
public void Show(DialogueTextTrack track, IReadOnlyLifetime lifetime) {
    _currentFrame.View(lifetime, frame => {
        SetSchemeFrames(track);  // Updates on every frame change
    });
}
```

**Rule:** Always subscribe to frame changes in timeline editors.

---

## Accumulation Log

| # | Date | File | Mistake | Lesson | Status |
|---|------|------|---------|--------|--------|
| 1 | 2026-01-25 | ObjectEditAnimationPlayTypeSelector.cs | Missing ISceneService, IScopeSetup, Create() | MonoBehaviour registration | Fixed |
| 2 | 2026-02-10 | DialogueTextView.cs | Dialogue text not updating on frame change | Frame synchronization | Fixed |

---

## Key Takeaways

1. **MonoBehaviour:** ISceneService + IScopeSetup + Create() + OnSetup()
2. **Subscriptions:** Always add Lifetime
3. **UI:** Always use View() not Advise()
4. **Collections:** Always use item.Lifetime
5. **Documentation:** Update immediately after finding pattern

---

## Lesson: new Lifetime() in Tests (CRITICAL)

### Mistake Made
```csharp
// WRONG — orphan lifetime, won't be terminated if test fails
var lifetime = new Lifetime();
balancer.Run(lifetime);
// ... test logic ...
lifetime.Terminate(); // never reached if test throws above
```

### Correct Pattern
```csharp
// CORRECT — handle.Lifetime is auto-terminated by ClusterTestRoot
balancer.Run(handle.Lifetime);
```

### Why This Matters
ClusterTestRoot creates a Lifetime for each test run and terminates it after Run() completes (whether success or failure). Using `new Lifetime()` bypasses this — if the test throws an exception before manual `Terminate()`, background loops and subscriptions keep running forever, leaking resources and potentially affecting other tests.

### Rule
NEVER use `new Lifetime()` in tests. Always use `handle.Lifetime` or `handle.Lifetime.Child()`.

---

## Related Documentation
- **Container Details:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
- **Lifetimes:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
