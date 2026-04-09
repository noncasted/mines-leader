# Code Style Rules

Full examples: → [docs/CODE_STYLE_FULL.md](../docs/CODE_STYLE_FULL.md)

## Member Order (MANDATORY)
Constructor → Private fields (readonly first) → Public methods → Private methods → Local functions

## Field Naming: _camelCase, no abbreviations
Wrong: `_hp`, `_ab`. Correct: `_health`, `_abilities`.

## Method Logic Structure
1. Fast path (cache check)
2. Creation
3. Setup / configure dependencies
4. GC.KeepAlive (for critical objects)
5. Side effects (add to collections)
6. Return
7. Local functions

## Collection Patterns
- Initialize inline: `private List<Item> _items = new();`
- Lookup: `TryGetValue` not `ContainsKey + []` (single lookup)

## Never Null-Check [SerializeField] Fields (CRITICAL)
Wrong: `if (_label != null) _label.text = "X";` — silently hides broken prefab.
Correct: `_label.text = "X";` — crash immediately if field not assigned in Inspector.
Rule: serialized fields MUST be assigned. Null guard hides misconfiguration. Let NullRef crash loudly.

## Exception Handling: Graceful
Catch → log with `[ClassName]` prefix → don't rethrow. App must survive.

## Braces: Always Same Line
```csharp
public class X {
    public void M() {
        if (cond) { }
    }
}
```

## Fire-and-Forget UniTask: .NoAwait()
Wrong: calling async method from sync context without awaiting (compiler warning).
Correct: `StartAnimation().NoAwait()` — explicit intent, suppresses warning.
Use only when: you don't need result and don't need to handle exceptions.
