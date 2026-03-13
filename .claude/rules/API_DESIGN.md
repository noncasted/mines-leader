# API Design Rules

Full examples: → [docs/API_DESIGN_FULL.md](../docs/API_DESIGN_FULL.md)

## Rule 1: No Async suffix for UniTask
`UniTask<T>` already signals async. Wrong: `LoadCharacterAsync()`. Correct: `LoadCharacter()`.

## Rule 2: Return IReadOnlyList, not array
Wrong: `Item[] GetInventory()`. Correct: `IReadOnlyList<Item> GetInventory() => _items.AsReadOnly()`.

## Rule 3: Return empty collection, not null
Wrong: `return null`. Correct: `return Array.Empty<T>()`. Callers can iterate without null checks.

## Rule 4: Catch ALL file errors
```csharp
try { /* load */ }
catch {
    UnityEngine.Object.Destroy(texture); // Full name — avoids namespace conflict
    Debug.LogError($"Failed: {path}");
    return null; // Don't throw — allows batch loading to continue
}
```

## Rule 5: Wrap callbacks with UniTask
Poll with `await UniTask.Delay(16)` until callback fires. Never block the main thread.
