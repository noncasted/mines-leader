# Reactive: Patterns & Real-World Usage

**For advanced reactive scenarios. Read basics docs first.**

## TL;DR
- UI Binding: `View()` always
- Collections: `View()` not `Advise()`
- Item subscriptions: Use `item.Lifetime`
- Async: Use `WaitTrue()`, `WaitFalse()`

## Pattern 1: UI Binding

```csharp
public void OnSetup(IReadOnlyLifetime lifetime) {
    // Single value
    _character.Health.View(lifetime, health => {
        _slider.value = health / 100f;
    });

    // Collection
    _inventory.Items.View(lifetime, item => {
        CreateItemUI(item);
        item.Lifetime.Listen(() => DestroyItemUI(item));
    });

    // Conditional UI
    _isPaused.View(lifetime, isPaused => {
        _menu.SetActive(isPaused);
    });
}
```

## Pattern 2: Item-Scoped Subscriptions

```csharp
public void OnSetup(IReadOnlyLifetime lifetime) {
    _characters.View(lifetime, character => {
        // Subscribe with item's lifetime
        // Auto-cleanup when character dies
        character.Health.View(character.Lifetime, health => {
            UpdateHealthBar(character, health);
        });

        character.OnDamaged.Advise(character.Lifetime, () => {
            PlayDamageAnimation(character);
        });
    });
}
```

## Pattern 3: State Machines

```csharp
// Simple boolean states
var isAlerted = new ViewableProperty<bool>(false);
isAlerted.AdviseTrue(lifetime, () => StartSearching());

// Enum states
var state = new ViewableProperty<CharacterState>(Idle);
state.View(lifetime, s => {
    switch (s) {
        case Idle: StopMovement(); break;
        case Moving: StartMovement(); break;
        case Attacking: PlayAttack(); break;
    }
});

// Derived state (computed from multiple properties)
_health.Advise(lifetime, _ => UpdateCanCast());
_mana.Advise(lifetime, _ => UpdateCanCast());

void UpdateCanCast() {
    bool canCast = _health.Value > 10 && _mana.Value >= 20;
    _isCanCast.Set(canCast);
}
```

## Pattern 4: Async Coordination

```csharp
// Wait for condition
public async void LoadLevel(IReadOnlyLifetime lifetime) {
    await _isReady.WaitTrue(lifetime);
    StartGameplay();
}

// Conditional async
public async void ProcessDialogue(IReadOnlyLifetime lifetime) {
    PlayBasicDialogue();
    if (await _unlockedAdvanced.WaitTrue(lifetime)) {
        PlayAdvancedDialogue();
    }
}

// Race condition
while (!appLifetime.IsTerminated) {
    await UniTask.Delay(1000, cancellationToken: appLifetime.Token);
    if (!_isActive.Value || _health.Value <= 0) break;
    DealDamage();
}
```

## Common Mistakes

❌ Wrong lifetime for item subscriptions:
```
items.View(sceneLifetime, item => {
    item.Events.Advise(sceneLifetime, handler);
});
```
(Memory leak when item removed)

✅ Correct:
```
items.View(sceneLifetime, item => {
    item.Events.Advise(item.Lifetime, handler);
});
```

❌ Using Advise instead of View for UI:
```
_health.Advise(uiLifetime, health => { });
```
(UI doesn't show initial value)

✅ Correct:
```
_health.View(uiLifetime, health => { });
```

## Next Steps
- **Events:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **Values:** [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md)
- **Collections:** [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md)
- **Lifetimes:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
