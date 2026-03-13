# Errors & Fixes: Quick Lookup Table

Fast reference table for common errors, their causes, and fixes.

---

## Runtime Errors

| Error Message | Root Cause | Key File | Fix |
|---|---|---|---|
| `NullReferenceException` in OnSetup() | Lifetime parameter is null | [MONOBEHAVIOUR.md](../rules/MONOBEHAVIOUR.md) | OnSetup signature must be `void OnSetup(IReadOnlyLifetime lt)` |
| Callback never fires | Missing Lifetime on subscription | [LIFETIMES.md](../rules/LIFETIMES.md) | Add lifetime: `event.Advise(lifetime, callback)` |
| UI text doesn't show initial value | Using `Advise()` instead of `View()` | [REACTIVE.md](../rules/REACTIVE.md), [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md) | Change to `View()`: `property.View(lifetime, val => text.text = val)` |
| Object reference disappears | Item subscription using wrong lifetime | [COMMON_MISTAKES.md](../rules/COMMON_MISTAKES.md) Mistake 4 | Use `item.Lifetime` for item-level subscriptions |
| Task exceptions swallowed | Async method not awaited | [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md) | Use `.NoAwait()` if fire-and-forget, else `await` |

---

## Memory Leaks

| Leak Pattern | Why It Leaks | Key File | Fix |
|---|---|---|---|
| `event.Advise(null, callback)` | Null lifetime = no cleanup | [LIFETIMES.md](../rules/LIFETIMES.md) | NEVER pass null: `event.Advise(lifetime, callback)` |
| `items.View(parentLifetime, item => item.Events.Advise(parentLifetime, ...))` | Item subscriptions survive item removal | [COMMON_MISTAKES.md](../rules/COMMON_MISTAKES.md) Mistake 4 | Use `item.Events.Advise(item.Lifetime, ...)` |
| Lifetime never terminated | Manual lifetime created but not cleaned up | [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md) | Call `lifetime.Terminate()` when done |
| Double Lifetime creation in loop | Creating new Lifetime per iteration | [COMMON_LIFETIMES_PATTERNS.md](COMMON_LIFETIMES_PATTERNS.md) | Reuse lifetime: `for (...) { Advise(lifetime, ...) }` |

---

## Architecture & Registration Errors

| Error | Cause | Key File | Fix |
|---|---|---|---|
| `OnSetup()` never called | Missing ISceneService or IScopeSetup | [MONOBEHAVIOUR.md](../rules/MONOBEHAVIOUR.md) | Implement both interfaces + Create() + register |
| `[Inject]` field stays null | Not registered in DI container | [COMMON_CONTAINER.md](COMMON_CONTAINER.md) | Register in Create(): `builder.RegisterComponent(this).As<IService>()` |
| Circular dependency between services | A→B→A reference | [COMMON_CONTAINER.md](COMMON_CONTAINER.md) | Refactor to inject Lifetime instead of direct reference |
| Service initialized twice | Multiple registrations | [COMMON_CONTAINER.md](COMMON_CONTAINER.md) | Remove duplicate `builder.RegisterComponent()` calls |

---

## Build & Compilation Errors

| Error | Cause | Key File | Fix |
|---|---|---|---|
| New .cs file silently not compiled | File not added to .csproj (Unity doesn't auto-discover) | [COMMON_MISTAKES.md](../rules/COMMON_MISTAKES.md) #6 | 1. Find correct csproj: `grep -rl "SimilarFile.cs" *.csproj` 2. Add `<Compile Include="Path\To\NewFile.cs" />` inside `<ItemGroup>` |
| Missing using statements in docs | Path reference uses old location | [TRIGGERS.md](TRIGGERS.md) | Update to `.claude/docs/` or `.claude/rules/` paths |
| Compiler warning: async not awaited | Fire-and-forget without .NoAwait() | [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md) | Add `.NoAwait()` to suppress: `method().NoAwait()` |

---

## Dialogue System Errors

| Error | Cause | Related Docs | Fix |
|---|---|---|---|
| Dialogue text not updated on frame change | View() not using current Lifetime | MEMORY: dialogue | Re-subscribe with current lifetime in SetSchemeFrames() |
| Position/Scale changes lost on save | OnObjectSave() doesn't capture Position/Scale | MEMORY: dialogue | Extend OnObjectSave() to set Frame.Position + Frame.Scale |
| DialogueView shows no options | GetTracks<DialogueOptionTrack>() returns empty | MEMORY: dialogue | Check ObjectAnimationScheme has DialogueOptionTrack added |
| Color tag not rendered | SetSchemeFrames() forgot rich text syntax | MEMORY: dialogue_color_system.md | Use `<color=#RRGGBB>text</color>` in TMP_Text |

---

## Collection & Iteration Errors

| Error | Cause | Key File | Fix |
|---|---|---|---|
| Collection modified during iteration | Remove() called inside View() lambda | [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md) | Schedule removal for next frame or collect then remove |
| Count mismatch after Add/Remove | Async operation in progress | [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md) | Use ViewableList operations, not direct List |

---

## How to Use This Table

1. **See an error?** → Search first column
2. **Find your case** → Read "Why It Leaks" or "Cause"
3. **Get the fix** → Click Key File → then use Fix column
4. **Need details?** → Each Key File has full explanation + examples

---

## Error Investigation Checklist

**Memory leak suspicion?**
- [ ] Every `Advise()` has non-null lifetime? (Check [LIFETIMES.md](../rules/LIFETIMES.md))
- [ ] Item subscriptions use `item.Lifetime`? (Check [COMMON_MISTAKES.md](../rules/COMMON_MISTAKES.md) Mistake 4)
- [ ] Manual `new Lifetime()` gets terminated? (Check [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md))

**Callback never fires?**
- [ ] Lifetime not terminated yet? (Check with `lifetime.IsTerminated`)
- [ ] Wrong Lifetime passed? (Check [DECISION_TREES.md](DECISION_TREES.md) #2)
- [ ] Using `Advise()` for UI? (Should be `View()` per [DECISION_TREES.md](DECISION_TREES.md) #5)

**Service not initializing?**
- [ ] Implements ISceneService + IScopeSetup? (Check [MONOBEHAVIOUR.md](../rules/MONOBEHAVIOUR.md))
- [ ] Has Create() method? (Check [COMMON_MISTAKES.md](../rules/COMMON_MISTAKES.md) Mistake 1)
- [ ] Registered with builder? (Check [COMMON_CONTAINER.md](COMMON_CONTAINER.md))

---

## Adding New Errors

When you discover a new error pattern:
1. Add row to appropriate section above
2. Link to relevant .md file
3. Update CLAUDE_MISTAKES.md if it's a common mistake
4. Update memory if it's project-specific

For examples:
→ [CODE_EXAMPLES.md](CODE_EXAMPLES.md) - all working code patterns
→ [COMMON_MISTAKES.md](../rules/COMMON_MISTAKES.md) - top 5 mistakes
