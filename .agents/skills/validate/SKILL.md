# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /validate, validate, валидация, валидируй, проверь правила, validate files, проверь по правилам, project rules, code validation
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Validate Skill

When the user runs `/validate`, check all modified `.cs` files against project rules and report every violation.

---

## Execution Steps

### Step 1 — Get changed files

Run both commands, merge results, deduplicate:
```
git diff --name-only HEAD
git diff --name-only --cached
```
Filter to `.cs` files only. Skip: `.meta`, `.prefab`, `.unity`, `.asset`, `.csproj`.

If no `.cs` files changed — report "Нет изменённых .cs файлов" and stop.

### Step 2 — Load rules

Always read:
- `.agents/docs/CODE_STYLE_FULL.md`
- `.agents/docs/CLAUDE_MISTAKES.md`

Read these only if relevant files are found:
- `.agents/docs/COMMON_CONTAINER.md` — if any file extends `MonoBehaviour`
- `.agents/docs/COMMON_LIFETIMES.md` — if any file uses `Advise`, `View`, `ListenClick`, `Lifetime`
- `.agents/docs/COMMON_REACTIVE_BASICS.md` — if any file uses `EventSource`, `ViewableProperty`, `ViewableList`
- `.agents/docs/API_DESIGN_FULL.md` — if any file has `UniTask`, `async`, collection return types

### Step 3 — Validate each file

Read each `.cs` file in full. Check every applicable rule:

**MonoBehaviour** (only if `MonoBehaviour` in class declaration):
- [ ] Implements `ISceneService`
- [ ] Implements `IScopeSetup` (if `OnSetup` method exists)
- [ ] Has `public void Create(IScopeBuilder builder)` method
- [ ] `Create()` calls `builder.RegisterComponent(this).As<IScopeSetup>()`
- [ ] No initialization in `Awake()` or `Start()` — must be in `OnSetup()`
- [ ] `[Inject]` fields are not accessed in `Create()` (injected after Create)

**Lifetimes**:
- [ ] Every `Advise(...)` call has a non-null lifetime as first argument
- [ ] Every `View(...)` call has a non-null lifetime as first argument
- [ ] Every `ListenClick(...)` call has a non-null lifetime as first argument
- [ ] Item subscriptions inside collection `View` use `item.Lifetime`, not the outer lifetime
- [ ] Manually created `new Lifetime()` is eventually terminated

**Reactive**:
- [ ] UI text/image/slider bindings use `View()`, not `Advise()`
- [ ] `EventSource` is not used to store state (use `ViewableProperty` instead)
- [ ] `ViewableList.View()` used when existing items must be shown, not `Advise()`

**Code Style**:
- [ ] Member order: constructor → readonly fields → mutable fields → public methods → private methods → local functions
- [ ] Field names: `_camelCase`, no abbreviations (`_hp` → `_health`, `_ab` → `_abilities`)
- [ ] Collections initialized inline: `private List<X> _items = new();`
- [ ] Dictionary lookup: `TryGetValue` not `ContainsKey + []`
- [ ] Braces on same line (`{` never on its own line)
- [ ] Fire-and-forget `UniTask` calls use `.NoAwait()`

**API Design**:
- [ ] No `Async` suffix on `UniTask` methods
- [ ] Collection return types are `IReadOnlyList<T>`, not `T[]`
- [ ] Methods returning collections never return `null` — use `Array.Empty<T>()`
- [ ] File I/O wrapped in `try/catch` with `Debug.LogError` and return `null`

### Step 4 — Output the report

---

## Output Format

```
## Validation Report

### `Assets/path/to/File.cs`
❌ **Lifetime leak** — `_events.Advise(null, ...)` (строка ~42): передай lifetime
❌ **UI binding** — `_health.Advise(lifetime, ...)` (строка ~67): для UI нужен View()
⚠️  **Naming** — поле `_hp` → `_health` (аббревиатура)
✅ MonoBehaviour pattern — complete
✅ Code style — OK

---

### `Assets/path/to/Other.cs`
✅ Все проверки пройдены

---

## Итог
Файлов проверено: N
❌ Ошибок: X
⚠️  Предупреждений: Y
```

---

## Severity

- `❌` — rule violation, must be fixed (memory leak, wrong pattern, broken initialization)
- `⚠️` — style issue, should be fixed (naming, ordering, minor style)
- `✅` — check passed

Include approximate line numbers wherever possible.
For each `❌`, write a one-line fix hint.
