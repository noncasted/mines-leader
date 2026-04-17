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

→ [COMMON_CONTAINER.md](COMMON_CONTAINER.md)

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

## Lesson 5: Bot Card Strategy Must Match Card's Internal Logic

### Mistake Made
```csharp
// WRONG — ZipZap strategy passes FREE cell position, but card searches mines in Rhombus (no diagonals)
Position GetPosition() {
    // Finds free cell with mine neighbors (including diagonal)
    return checkPosition; // Free cell, not the mine itself
}
```

```csharp
// WRONG — OpponentBomb strategy searches BOT's board, but card operates on OPPONENT's board
var board = _context.Bot.Board; // Should be _context.Opponent.Board!
```

### Correct Pattern
```csharp
// CORRECT — pass the MINE position directly, card will find it via SearchRadius
Position FindMineTarget() {
    // Find unflagged mine with at least one adjacent free cell
    if (taken.HasMine && !taken.IsFlagged && hasAdjacentFree)
        return position; // Mine position, not free cell
}

// CORRECT — search the correct board
var board = _context.Opponent.Board; // Match what the card actually targets
```

**Rule:** Bot strategy's position-finding MUST match the card's internal search logic.
Always verify: which board does the card operate on? What search shape does it use?

---

## Lesson 6: Guard ViewNotNull Against Initialization Triggers

### Mistake Made
```csharp
// WRONG — _currentPlayer.Set(botPlayer) before round loop triggers ViewNotNull with Moves=0
_round.CurrentPlayer.ViewNotNull(user.Lifetime, (roundLifetime, player) => {
    Task.Run(() => OnBotTurn(roundLifetime)); // Fires with Moves=0!
});
```

### Correct Pattern
```csharp
// CORRECT — guard against premature trigger
_round.CurrentPlayer.ViewNotNull(user.Lifetime, (roundLifetime, player) => {
    if (player.Moves.IsAvailable == false) return; // Skip init trigger
    Task.Run(() => OnBotTurn(roundLifetime));
});
```

**Rule:** When ViewNotNull fires during object setup (before game loop starts), the state may not be fully initialized. Always guard with a readiness check.

---

## Accumulation Log

| # | Date | File | Mistake | Lesson | Status |
|---|------|------|---------|--------|--------|
| 1 | 2026-01-25 | ObjectEditAnimationPlayTypeSelector.cs | Missing ISceneService, IScopeSetup, Create() | MonoBehaviour registration | Fixed |
| 2 | 2026-02-10 | DialogueTextView.cs | Dialogue text not updating on frame change | Frame synchronization | Fixed |
| 3 | 2026-04-15 | ZipZapStrategy.cs, OpponentBombStrategy.cs | Strategy position/board mismatch with card logic | Card strategy must match card internals | Fixed |
| 4 | 2026-04-15 | BotRunner.cs | ViewNotNull fires before moves restored | Guard against init triggers | Fixed |

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

## Lesson 7: Every Mutation Needs an Explicit Record* Call

### Mistake Made

Relying on the old `MoveSnapshot.HandleBoards/HandlePlayers` auto-subscribe
mechanism, or forgetting a `Record*` call after mutating cells/mana/health/moves/modifiers.

```csharp
// WRONG — resource is mutated but nothing is recorded.
invoker.Mana.Use(manaCost);
// result: client still sees the old mana value; diff-guard throws in tests.
```

### Correct Pattern

Every state-changing line in a command or card body is paired with a `Record*`
call on the snapshot the engine handed over (`CardUseContext.Snapshot` for cards,
`Context.Snapshot` for commands, explicit parameter for round actions).

```csharp
// CORRECT
invoker.Mana.Use(manaCost);
snapshot.RecordManaUpdate(invoker);

// CORRECT — modifier affects derived Max, so re-record the resource too.
invoker.Modifiers.Inc(PlayerModifier.AdditionalMana, amount);
snapshot.RecordModifierUpdate(invoker, PlayerModifier.AdditionalMana,
    invoker.Modifiers.Get(PlayerModifier.AdditionalMana));
invoker.Mana.SetCurrent(invoker.Mana.Current + amount);
snapshot.RecordManaUpdate(invoker);

// CORRECT (reveal-card) — backend mutates silently, two parallel lists travel
// via CardActionSnapshot: OpenedCells drives PlayCellAction animation,
// UpdatedFreeCells drives cell.OnMinesUpdated on the client.
var revealed = board.Revealer.Reveal(targetPositions);
revealed.AddRange(minePositions);
var openedCells = revealed.Distinct().Select(p => new OpenedCell {
    Position = p, MinesAround = board.Cells[p].AsFree().MinesAround
}).ToList();
// For Bloodhound/ErosionDozer/ZipZap add neighbours whose MinesAround changed.
var updatedFreeCells = revealed.Concat(board.GetFreeNeighbours(minePositions))
    .Distinct().Select(p => new OpenedCell {
        Position = p, MinesAround = board.Cells[p].AsFree().MinesAround
    }).ToList();
snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Bloodhound
{
    TargetPlayer = board.OwnerId,
    TargetCells = targetPositions,
    OpenedCells = openedCells,
    UpdatedFreeCells = updatedFreeCells
});
```

### Rule

- Never mutate `Board` / `Player.Mana` / `Health` / `Moves` / `Modifiers` / `Hand`
  inside a command or card without a matching `snapshot.Record*` call on the next line.
- When a modifier influences derived `Max` (`AdditionalMana`, `AdditionalHealth`,
  `AdditionalMoves`), follow `RecordModifierUpdate` with the resource's own
  record (or let the `IModifiers.Set` cascade handle it automatically).
- **`RecordCardUse` must be the FIRST record** a card writes. All other derived
  records (`RecordCellTaken`, `RecordFlag`, `RecordEffectAdded`, `RecordMines`,
  etc.) must be written **after** the `RecordCardUse` call.
- **Cards that change the mine layout** (add/remove mines, reveal cells,
  set/remove flags) must always call
  `snapshot.RecordMines(board, board.MinesScanner.Recalculate(snapshot))` as
  the last board record — after `RecordCardUse`. The `snapshot` argument lets
  the scanner auto-write `BoardStateUpdate` when mine/flag totals change.
- **Reveal-cards** (Bloodhound, ChaosDiamond, ChaosScout, ErosionDozer,
  Excavator, MinefieldScout, OpponentBomb, ZipZap): call
  `board.Revealer.Reveal(positions)` directly — do NOT use
  `snapshot.RecordReveal`. Expose opened positions through two parallel
  fields on `CardActionSnapshot.X`: `OpenedCells` (только реально открытые,
  для анимации `PlayCellAction`) и `UpdatedFreeCells` (opened + соседи с
  изменённым `MinesAround`, для `cell.OnMinesUpdated` на клиенте). Для
  OpponentBomb/MinefieldScout/Excavator/ChaosDiamond/ChaosScout
  `UpdatedFreeCells = OpenedCells`; соседи подмешивают только
  Bloodhound/ErosionDozer/ZipZap через `board.GetFreeNeighbours(...)`.
- For non-card sites (`OpenCellCommand`, `OpenMultipleCellsCommand`,
  `BotCellAction`), the extension `snapshot.RecordReveal(board, positions)`
  remains — it records reveals as `CellFree`/`MinesAround` for the client.
- Cards that add mines to Free cells combine `RecordCellTaken` with
  `snapshot.RecordMines(board, board.MinesScanner.Recalculate(snapshot))` for
  neighbour recount (note the `snapshot` argument).
- `SnapshotDiffGuard` (enabled in tests + togglable from the Features console)
  will throw `SnapshotDiffException` listing exactly which field was mutated
  without a record.

→ [GAMEPLAY.md §Snapshot Sync](GAMEPLAY.md#snapshot-sync)

---

## Lesson 8: Snapshot Record Order — `CardRemove` After `RecordCardUse`

### Wrong

```csharp
// Backend (CardUseCommand): CardRemove inserted into prefix, BEFORE RecordCardUse
using (context.Snapshot.BeginInsertAt(prefixMark))
{
    player.Hand.Remove(cardId);
    context.Snapshot.RecordCardRemove(player.User.Id, cardId);  // prefix
    player.Mana.Use(context.Snapshot, manaCost);
    player.Moves.OnUsed(context.Snapshot);
}
// Card itself wrote RecordCardUse via context.Snapshot inside Use()
```

Client then applies records sequentially:

```csharp
// CardRemoveSnapshotHandler
var card = player.Hand.Entries.First(c => c.Id == record.CardId);
await card.Destroy();  // card gone from Hand

// CardActionSnapshotHandler (for RecordCardUse that follows)
var card = player.Hand.Entries.First(t => t.Id == record.CardId)!; // THROWS — sequence empty
```

### Correct

```csharp
// Only Mana + Moves in prefix; CardRemove appended AFTER card records
using (context.Snapshot.BeginInsertAt(prefixMark))
{
    player.Mana.Use(context.Snapshot, manaCost);
    player.Moves.OnUsed(context.Snapshot);
}

player.Hand.Remove(cardId);
context.Snapshot.RecordCardRemove(player.User.Id, cardId);

player.Stash.Add(handCard.Type);
context.Snapshot.RecordCardAdd(player.User.Id, cardId, handCard.Type, isStash: true);
```

### Rule

- **`RecordCardUse` must reach the client while the card entity is still in
  `Hand`.** The client's `CardActionSnapshotHandler` looks the card up by
  `record.CardId` to drive the play-out animation.
- Keep the final record order: `Mana → Moves → [card's side-effect records incl.
  RecordCardUse] → CardRemove → CardAdd(stash)`.
- Inserting `CardRemove` into the prefix is a subtle mismatch — the backend
  tests pass (they don't exercise client sync), but the Unity client throws
  `InvalidOperationException: Sequence contains no matching element`.

→ [GAMEPLAY.md §Card Lifecycle (Backend)](GAMEPLAY.md#card-lifecycle-backend)

---

## Related Documentation
- **Container Details:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
- **Lifetimes:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
