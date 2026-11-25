# Errors & Fixes: Quick Lookup Table

Fast reference table for common errors, their causes, and fixes.

---

## Runtime Errors

| Error Message | Root Cause | Key File | Fix |
|---|---|---|---|
| `NullReferenceException` in OnSetup() | Lifetime parameter is null | `tools/framework/.claude/docs/COMMON_CONTAINER.md` | OnSetup signature must be `void OnSetup(IReadOnlyLifetime lt)` |
| Callback never fires | Missing Lifetime on subscription | `tools/framework/.claude/docs/COMMON_LIFETIMES.md` | Add lifetime: `event.Advise(lifetime, callback)` |
| UI text doesn't show initial value | Using `Advise()` instead of `View()` | `tools/framework/.claude/docs/COMMON_REACTIVE_VALUES.md` | Change to `View()`: `property.View(lifetime, val => text.text = val)` |
| Object reference disappears | Item subscription using wrong lifetime | [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) Mistake 4 | Use `item.Lifetime` for item-level subscriptions |
| Task exceptions swallowed | Async method not awaited | `tools/framework/.claude/docs/COMMON_REACTIVE_PATTERNS.md` | Use `.NoAwait()` if fire-and-forget, else `await` |
| `Cell (x, y) is not on the board of player ... (size ...)` | A snapshot names a cell outside the synced board (`IBoard.GetSyncedCell`): client and backend boards differ in size, or the menu preview scenario is bigger than the 8x8 menu board | `client/Assets/GamePlay/Boards/Root/IBoard.cs` | Find who sent the position (snapshot record, card payload); fix the size of the board or the scenario, never skip the cell |
| `Cell ... of the target board is not on the board of the owner` | Dimension Rift pattern: the boards of the two players have different sizes | `client/Assets/GamePlay/Cards/Entities/Actions/CrossBoard/CardDimensionRiftAction.cs` | Boards of one match must be the same size; check the board config of the mode |
| `Cell ... has no view of effect ...` | A `CellEffectType` without an entry in `_effects` of `Cell_Base.prefab` (or a variant overrides the list); checked for every type when the board is set up (`CellEffects.View`) | `client/Assets/GamePlay/Boards/Cells/Effects/CellEffects.cs` | Add the view of the new effect to `Cell_Base.prefab` |
| `No card action sync is registered for ...` | A new card payload has no `ICardActionSync` registration | `client/Assets/GamePlay/Cards/Entities/Actions/Common/CardActionSyncDispatcher.cs` | Register the action of the card (`create-card` skill); a duplicate registration throws on `Add` |
| `[Snapshot] A record failed to apply: the match ends with an error` (the player sees "Match error" and goes to the menu) | A snapshot handler threw (its exception is attached: a cell off the board, a card not in the hand, ...), a record type has no handler (`[Snapshot] No handler for the record type ...`), or a handler of a record applied at once (flag, round of the same player) did not complete synchronously | `client/Assets/GamePlay/Services/Sync/SnapshotReceiver.cs` | Fix the cause in the attached exception; a new record type needs `AddSnapshotHandler` in `GamePlaySyncExtensions`; never catch inside the handler to keep the match going |
| `Card ... of player ... is neither in the hand nor on the table` | `CardRemove` came for a card the client never got or already removed (snapshot order) | `client/Assets/GamePlay/Services/Sync/CardRemoveSnapshotHandler.cs` | Check the order of `CardUse` / `CardRemove` / `CardsStashed` the backend writes |
| `Mirror Match of ... came without the copied action` | The backend action of the copied card wrote no `CardUse` | `client/Assets/GamePlay/Cards/Entities/Actions/Hand/CardMirrorMatchAction.cs` | Make the backend card write its `CardUse` snapshot |
| `Game mode ... is available on the backend but unknown to the client` | The backend config has a mode with no client description in `IGameModesRegistry` | `client/Assets/Menu/Play/MenuPlay.cs` | Add the mode to the client registry or remove it from the backend config |
| `VFX prefab of type ... is not set` | A VFX reference of a card action / menu preview is empty in the prefab or options | `client/Assets/GamePlay/Cards/Entities/Actions/Common/CardVfxFactory.cs`, `client/Assets/Menu/Decks/Preview/Sync/MenuPreviewVfxFactory.cs` | Assign the prefab |
| `The match results have no board of the loser ...` | `GameCompletedRecord.Boards` lacks the board of the loser (not a Leave result) | `client/Assets/GamePlay/Boards/Reveal/BoardsReveal.cs` | Check `MoveSnapshot.Completion.cs` of the backend |

---

## Memory Leaks

| Leak Pattern | Why It Leaks | Key File | Fix |
|---|---|---|---|
| `event.Advise(null, callback)` | Null lifetime = no cleanup | `tools/framework/.claude/docs/COMMON_LIFETIMES.md` | NEVER pass null: `event.Advise(lifetime, callback)` |
| `items.View(parentLifetime, item => item.Events.Advise(parentLifetime, ...))` | Item subscriptions survive item removal | [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) Mistake 4 | Use `item.Events.Advise(item.Lifetime, ...)` |
| Lifetime never terminated | Manual lifetime created but not cleaned up | `tools/framework/.claude/docs/COMMON_LIFETIMES.md` | Call `lifetime.Terminate()` when done |
| Double Lifetime creation in loop | Creating new Lifetime per iteration | `tools/framework/.claude/docs/COMMON_LIFETIMES_PATTERNS.md` | Reuse lifetime: `for (...) { Advise(lifetime, ...) }` |

---

## Architecture & Registration Errors

| Error | Cause | Key File | Fix |
|---|---|---|---|
| `OnSetup()` never called | Missing ISceneService or IScopeSetup | `tools/framework/.claude/docs/COMMON_CONTAINER.md` | Implement both interfaces + Create() + register |
| `[Inject]` field stays null | Not registered in DI container | `tools/framework/.claude/docs/COMMON_CONTAINER.md` | Register in Create(): `builder.RegisterComponent(this).As<IService>()` |
| Circular dependency between services | A→B→A reference | `tools/framework/.claude/docs/COMMON_CONTAINER.md` | Refactor to inject Lifetime instead of direct reference |
| Service initialized twice | Multiple registrations | `tools/framework/.claude/docs/COMMON_CONTAINER.md` | Remove duplicate `builder.RegisterComponent()` calls |

---

## Build & Compilation Errors

| Error | Cause | Key File | Fix |
|---|---|---|---|
| New .cs file silently not compiled | File not added to .csproj (Unity doesn't auto-discover) | [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) #6 | 1. Find correct csproj: `grep -rl "SimilarFile.cs" *.csproj` 2. Add `<Compile Include="Path\To\NewFile.cs" />` inside `<ItemGroup>` |
| Missing using statements in docs | Path reference uses old location | [TRIGGERS.md](TRIGGERS.md) | Update to `.claude/docs/` paths |
| Compiler warning: async not awaited | Fire-and-forget without .NoAwait() | `tools/framework/.claude/docs/COMMON_REACTIVE_PATTERNS.md` | Add `.NoAwait()` to suppress: `method().NoAwait()` |

---

## Dialogue System Errors

| Error | Cause | Related Docs | Fix |
|---|---|---|---|
| Dialogue text not updated on frame change | View() not using current Lifetime | MEMORY: dialogue | Re-subscribe with current lifetime in SetSchemeFrames() |
| Position/Scale changes lost on save | OnObjectSave() doesn't capture Position/Scale | MEMORY: dialogue | Extend OnObjectSave() to set Frame.Position + Frame.Scale |
| DialogueView shows no options | GetTracks<DialogueOptionTrack>() returns empty | MEMORY: dialogue | Check ObjectAnimationScheme has DialogueOptionTrack added |
| Color tag not rendered | SetSchemeFrames() forgot rich text syntax | MEMORY: dialogue_color_system.md | Use `<color=#RRGGBB>text</color>` in TMP_Text |

---

## Test Errors

| Error / Symptom | Root Cause | Key File | Fix |
|---|---|---|---|
| `--filter "FullyQualifiedName~..."` is silently ignored | xUnit v2 syntax, dropped in v3 | [TESTING.md](TESTING.md) | Use `-- --filter-class "*ClassName"` (note the `--` separator) |
| `TestResults/*.log` unreadable (mojibake / binary garbage) | xUnit v3 writes UTF-16LE, Read tool cannot parse | [TESTING.md](TESTING.md) | Run `tools/scripts/get-test-log.sh`, then Read `*.utf8.log` files |
| No log files created after `dotnet test` | Missing `UseMicrosoftTestingPlatformRunner` in .csproj | [TESTING.md](TESTING.md) | Add `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` + `<OutputType>Exe</OutputType>` |
| Tests hang / timeout | Deadlock, infinite loop, bad SQL, `Task.Delay` as wait | [TESTING.md](TESTING.md) | Find root cause in code; never increase timeouts blindly |
| `xunit.runner.json` settings ignored | File not copied to output | [TESTING.md](TESTING.md) | Add `<Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest"/>` |

---

## Collection & Iteration Errors

| Error | Cause | Key File | Fix |
|---|---|---|---|
| Collection modified during iteration | Remove() called inside View() lambda | `tools/framework/.claude/docs/COMMON_REACTIVE_COLLECTIONS.md` | Schedule removal for next frame or collect then remove |
| Count mismatch after Add/Remove | Async operation in progress | `tools/framework/.claude/docs/COMMON_REACTIVE_PATTERNS.md` | Use ViewableList operations, not direct List |

---

## How to Use This Table

1. **See an error?** → Search first column
2. **Find your case** → Read "Why It Leaks" or "Cause"
3. **Get the fix** → Click Key File → then use Fix column
4. **Need details?** → Each Key File has full explanation + examples

---

## Error Investigation Checklist

**Memory leak suspicion?**
- [ ] Every `Advise()` has non-null lifetime? (Check `tools/framework/.claude/docs/COMMON_LIFETIMES.md`)
- [ ] Item subscriptions use `item.Lifetime`? (Check [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) Mistake 4)
- [ ] Manual `new Lifetime()` gets terminated? (Check `tools/framework/.claude/docs/COMMON_LIFETIMES.md`)

**Callback never fires?**
- [ ] Lifetime not terminated yet? (Check with `lifetime.IsTerminated`)
- [ ] Wrong Lifetime passed? (Check `tools/framework/.claude/docs/DECISION_TREES.md` #2)
- [ ] Using `Advise()` for UI? (Should be `View()` per `tools/framework/.claude/docs/DECISION_TREES.md` #5)

**Service not initializing?**
- [ ] Implements ISceneService + IScopeSetup? (Check `tools/framework/.claude/docs/COMMON_CONTAINER.md`)
- [ ] Has Create() method? (Check [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) Mistake 1)
- [ ] Registered with builder? (Check `tools/framework/.claude/docs/COMMON_CONTAINER.md`)

---

## Adding New Errors

When you discover a new error pattern:
1. Add row to appropriate section above
2. Link to relevant .md file
3. Update CLAUDE_MISTAKES.md if it's a common mistake
4. Update memory if it's project-specific

For examples:
→ [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) - top 5 mistakes
