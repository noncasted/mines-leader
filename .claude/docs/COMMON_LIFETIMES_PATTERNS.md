# Lifetimes: Advanced Patterns

**For complex lifetime scenarios. Read [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md) first.**

## TL;DR
- Hierarchy: Parent → Children (auto-cleanup)
- Intersection: Multiple parents → shortest wins
- Conditional: Choose lifetime based on condition

## Lifetime Hierarchy

```csharp
public void OnSetup(IReadOnlyLifetime sceneLifetime) {
    // Level 1: Characters
    var characterLifetime = sceneLifetime.Child();
    SpawnCharacter(characterLifetime);

    // When character dies → ability lifetimes also terminate
    private void SpawnCharacter(IReadOnlyLifetime characterLifetime) {
        // Level 2: Abilities for this character
        var abilityLifetime = characterLifetime.Child();
        AddAbility(abilityLifetime);
    }
}
// When scene ends → all cleanup cascades down
```

## Lifetime Intersection

```csharp
public void Activate(IReadOnlyLifetime abilityLt, IReadOnlyLifetime targetLt) {
    // Ability active while BOTH are alive
    var activeLt = abilityLt.Intersect(targetLt);

    targetHealthListener = targetLt.Advise(activeLt, OnHealthChanged);
}
// Stops if ability deactivates OR target dies
```

## Conditional Lifetimes

```csharp
public void SetupAudio(bool audioEnabled, IReadOnlyLifetime sceneLt) {
    // If disabled → use pre-terminated lifetime (no-op)
    var audioLt = audioEnabled
        ? sceneLt.Child()
        : TerminatedLifetime.Instance;

    _audioManager.ListenMusicChange(audioLt, OnMusicChanged);
    _audioManager.ListenSoundEffect(audioLt, OnSoundEffect);
    // If audioEnabled = false: no callbacks, no null checks
}
```

## Debugging Tips

❌ **Lifetime never terminates** → memory leak
- Check if `Terminate()` is called
- Use `lifetime.IsTerminated` to verify state

❌ **Can't cancel single operation?** → Create child lifetime
```csharp
var operationLt = sceneLifetime.Child();
LongRunningTask(operationLt);

// Later: cancel just this operation
(operationLt as ILifetime)?.Terminate();
```

❌ **Ungraceful shutdown** → Scope not fully disposed
- Use profiler to find active subscriptions
- Verify all lifetimes terminated

## Common Mistakes

❌ Sharing lifetime across unrelated scopes:
```
playScene.Advise(uiLifetime, OnGameplayEvent);
```

✅ Use separate lifetimes:
```
var gameplayLifetime = sceneLifetime.Child();
playScene.Advise(gameplayLifetime, OnGameplayEvent);
```

❌ Overly complex intersections:
```
var lt = abilityLt.Intersect(targetLt).Intersect(sceneLt);
```

✅ Use hierarchy if one is child of other

## Quick Reference

**Creating:**
```
new Lifetime()
parentLifetime.Child()
token.ToLifetime()
TerminatedLifetime.Instance
```

**Checking:**
```
lifetime.IsTerminated
lifetime.Token
```

**Cleanup:**
```
lifetime.Terminate()
lifetime.Listen(() => { })
lifetime.Intersect(otherLifetime)
```

## Key Takeaways

1. **Hierarchy** = default pattern
2. **Intersection** = when need shortest lifetime
3. **Conditional** = when feature may not run
4. Always verify lifetime terminates eventually
5. Use `IsTerminated` when unsure

## Related
- **Basics:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **DI:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
