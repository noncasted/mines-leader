# Code Style Guide

## Member Order (MANDATORY)

```csharp
public class MyService : MonoBehaviour {
    // 1. Constructor
    public MyService(IDependency dep) { }

    // 2. Private fields - readonly first, then mutable
    private readonly IDependency _dependency;
    private readonly EventSource<int> _onEvent = new();
    private int _counter;

    // 3. Public methods (interfaces)
    public void Create(IScopeBuilder builder) { }
    public void OnSetup(IReadOnlyLifetime lifetime) { }

    // 4. Private methods
    private void Helper() { }

    // 5. Local functions (inside methods)
}
```

## Field Naming: _camelCase

```csharp
// ✅ CORRECT
private int _health;
private List<Ability> _abilities;
private EventSource<int> _onDamaged = new();

// ❌ WRONG
private int _hp;              // Abbreviated
private List<Ability> _ab;    // Abbreviated
private EventSource<int> _damage; // Non-descriptive
```

Rules:
- Descriptive names (no abbreviations)
- Group readonly vs mutable
- Initialize collections inline

## Method Logic Structure

```csharp
public Character Create(CharacterTemplate template) {
    // 1. Fast path - return cached results
    if (_cache.TryGetValue(template.Id, out var cached)) {
        return cached;
    }

    // 2. Creation - instantiate new objects
    var character = new Character {
        Name = template.Name,
        Health = template.Health,
    };

    // 3. Setup - configure dependencies
    character.SetAbilities(LoadAbilities(template));

    // 4. Setup verification - GC.KeepAlive for critical objects
    GC.KeepAlive(character);

    // 5. Side effects - add to collections, register callbacks
    _characters.Add(character);
    _cache[template.Id] = character;

    // 6. Return
    return character;

    // 7. Local functions
    Ability[] LoadAbilities(CharacterTemplate t) => ...;
}
```

## Local Functions

Use for:
- Closures (capture method variables)
- Error handlers
- Retry logic
- Context-specific helpers

```csharp
public void Process(IReadOnlyLifetime lifetime) {
    ProcessData(data, ValidateEntry);

    void ProcessData(List<Entry> entries, Func<Entry, bool> validator) {
        var valid = entries.Where(validator).ToList();
        ApplyTransform(valid);
    }

    bool ValidateEntry(Entry entry) {
        return entry != null && entry.IsValid;
    }
}
```

## Exception Handling: Graceful Degradation

```csharp
try {
    _eventBus.Advise(lifetime, OnEvent);
}
catch (Exception ex) {
    Debug.LogError($"[{GetType().Name}] Subscribe failed: {ex}");
    // Return gracefully - don't crash app
}
```

## Collections: Initialize Inline

```csharp
// ✅ CORRECT
private List<Item> _items = new();
private Dictionary<string, Item> _map = new();

// ❌ WRONG
private List<Item> _items;
public MyService() {
    _items = new(); // Separate init
}
```

## Dictionary Lookup: TryGetValue

```csharp
// WRONG - two lookups
if (_map.ContainsKey(key)) {
    var value = _map[key]; // Second lookup
}

// CORRECT - one lookup
if (_map.TryGetValue(key, out var value)) {
    // Use value
}
```

## GC.KeepAlive Usage

```csharp
public Character Create(CharacterTemplate template) {
    var character = new Character { ... };
    character.SetAbilities(abilities);

    // Force keep in memory during critical section
    GC.KeepAlive(character);

    _characters.Add(character);
    return character;
}
```

## Braces: Always on Same Line

```csharp
// ✅ CORRECT (EditorConfig requirement)
public class MyService {
    public void Method() {
        if (condition) {
            DoSomething();
        }
    }
}

// ❌ WRONG
public class MyService
{
    public void Method()
    {
    }
}
```

## UniTask Fire-and-Forget: NoAwait

Use when you intentionally don't await a UniTask (fire-and-forget pattern):

```csharp
// ❌ WRONG - Compiler warning about async not awaited
public void StartAnimation() {
    PlayAnimation();
}

private async UniTask PlayAnimation() {
    await _animator.Play(_clip);
}

// ✅ CORRECT - Explicitly suppress warning
public void StartAnimation() {
    PlayAnimation().NoAwait();
}

private async UniTask PlayAnimation() {
    await _animator.Play(_clip);
}
```

**When to use:**
- ✅ Fire-and-forget operations (background animations, non-critical loading)
- ✅ Called from non-async context (Awake, OnClick, Update)
- ✅ You deliberately don't care about completion

**When NOT to use:**
- ❌ You need the result → use `await`
- ❌ You need to handle exceptions → use `await` in try-catch
- ❌ You need to wait for completion → use `await`

## Quick Checklist

- [ ] Member order: Constructor → Fields → Public → Private → Local
- [ ] Names: `_camelCase`, descriptive, no abbreviations
- [ ] Collections: Initialize inline
- [ ] Dictionary: Use `TryGetValue`
- [ ] Methods: Fast-path → Create → Setup → Side-effects → Return → Locals
- [ ] Exceptions: Log + return gracefully
- [ ] Braces: Always on same line
- [ ] Fire-and-forget UniTask: Add `.NoAwait()` to suppress warnings

## Related
- **API Design:** [API_DESIGN.md](../rules/API_DESIGN.md)
- **Container:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
