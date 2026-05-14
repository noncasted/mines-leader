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

## Lesson 9: Observer Grains Must Be `[Reentrant]`

### Mistake Made
```csharp
// WRONG — non-reentrant grain holding an observer reference
public class RuntimePipe : Grain, IRuntimePipe
{
    private IRuntimePipeObserver? _observer;

    public Task BindObserver(IRuntimePipeObserver observer) { _observer = observer; return Task.CompletedTask; }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        // Calls _observer.Send() — blocks for 30s when observer's client is dead.
        // Grain queue serializes every incoming call → new BindObserver waits behind
        // every stuck Send. Coordinator restart = 50s+ freeze across cluster.
    }
}
```

### Correct Pattern
```csharp
using Orleans.Concurrency;

[Reentrant]
public class RuntimePipe : Grain, IRuntimePipe
{
    public async Task<TResponse> Send<TResponse>(object message)
    {
        var observer = _observer; // snapshot
        try { return await observer!.Send<TResponse>(message).WaitAsync(timeout); }
        catch (Exception ex)
        {
            if (ReferenceEquals(_observer, observer)) _observer = null; // discard dead
            throw;
        }
    }
}
```

### Rule

- Any grain that forwards calls to an **observer reference whose owning process can die**
  must be `[Reentrant]` and must **discard** the observer on Send failure.
- A single stuck `_observer.Send(...)` on a non-reentrant grain blocks the entire queue —
  subsequent `BindObserver` from a fresh client cannot unstick it until Orleans' outer
  response timeout (50s) fires one-by-one for each queued message.
- Orleans gives disconnected clients a ~65s grace window before dropping them; during
  that window the grain will still route observer callbacks to the dead client.

→ [deploy-epoch.md §Фиксы пайпа](../../obsidian/architecture/deploy-epoch.md)

---

## Lesson 10: Do Not Wait For Your Own State Write

### Mistake Made
```csharp
// Coordinator process:
await _loop.OnLocalSetupCompleted(lifetime);    // calls grain.MarkCoordinatorReady() → true
// Then, symmetric with other services:
await WaitCoordinatorReady();                    // polls grain.GetState() for 55 seconds
                                                 // before seeing CoordinatorReady=true
```

The coordinator's Orleans client has just restarted; silo routes the poll through a
fresh client connection. Initialize/MarkCoordinatorReady succeeded (first burst), but
follow-up reads sat in some transient routing state until the silo dropped the previous
client (~65s grace). Meanwhile, other services (with their own clients) saw
`CoordinatorReady=true` instantly.

### Correct Pattern
```csharp
if (_discovery.Self.Tag == ServiceTag.Coordinator)
{
    // Coordinator just awaited grain.MarkCoordinatorReady() locally — it's already true.
}
else
{
    await WaitCoordinatorReady();
}
```

### Rule

- A process that just awaited its own grain write does not need to poll the grain to
  confirm it. Treat the local `await` as the acknowledgment.
- Symmetric startup code for coordinator and participants tempts you to make the
  coordinator "wait for itself" — it may mask latent Orleans routing hiccups for minutes.

→ [deploy-epoch.md §Диаграмма рестарта координатора](../../obsidian/architecture/deploy-epoch.md)

---

## Lesson: Do Not Hold Local Delivery Locks Across Orleans Observer Binding

### Mistake Made
```csharp
// WRONG — Publish can call observer.Send while this client waits for CatchUp.
await using var deliveryLock = await observer.LockDelivery();
await channel.AddObserver(observer.Id, observerRef);
var catchUp = await channel.CatchUp(observer.LastSeenSequence);
```

After removing `[AlwaysInterleave]`, a concurrent `Publish` grain turn can call
`observer.Send` and wait for the local lock while the client's `CatchUp` call is queued
behind that `Publish`. The result is a delivery-timeout deadlock pattern.

### Correct Pattern
```csharp
// CORRECT — buffer live deliveries; do not block the grain's observer.Send call.
observer.BeginBuffering();
try {
    await channel.AddObserver(observer.Id, observerRef);
    var catchUp = await channel.CatchUp(lastSeenSequence);
    await observer.ReplayCatchUp(catchUp.Messages);
}
finally {
    await observer.EndBuffering();
}
```

### Rule
When coordinating Orleans catch-up with live observer delivery, local synchronization
must not make `observer.Send` wait for a grain call queued behind the current `Publish`.
Buffer live deliveries and flush them after catch-up; use sequence filtering to skip
duplicates.

---

## Lesson 11: UI Toolkit Sprite Reference — Use `resource()` Inline, Not `url()` in USS

### Mistake Made
```css
/* WRONG — url() with GUID paths in USS for sprites */
background-image: url("project://Assets/GamePlay/Players/Art/Step.psd#step_button_1");
```
```css
/* WRONG — resource() in USS for multi-sprite PSDs misses the right sprite */
background-image: resource("GamePlay/UI/card_desc");
```
```xml
<!-- WRONG — arbitrary size, not matching sprite native dimensions -->
<VisualElement style="width: 142px; height: 142px;"/>
```
```css
/* WRONG — using Ithaca-LVB75 where BITACH is required */
-unity-font-definition: resource("Ithaca-LVB75");
```
```xml
<!-- WRONG — unnecessary nested container for background -->
<VisualElement name="round-button">
    <VisualElement name="round-bg" style="background-image: ..."/>
    <Label name="round-time"/>
</VisualElement>
```

### Correct Pattern
```xml
<!-- CORRECT — resource() inline, exact sprite size, font matches design -->
<VisualElement name="card-preview" style="background-image: resource(&quot;GamePlay/UI/card_desc&quot;); height: 50px; width: 38px;">
    <Label name="card-name" style="-unity-font-definition: resource(&quot;BITACH&quot;); font-size: 3px; -unity-text-align: lower-center; -unity-text-auto-size: best-fit 2px 4px;"/>
    <Label name="card-description" style="-unity-font-definition: resource(&quot;Ithaca-LVB75&quot;); font-size: 4px; -unity-text-align: upper-center;"/>
</VisualElement>
```
```xml
<!-- CORRECT — background on root element, no extra wrapper -->
<VisualElement name="round-button" style="background-image: resource(&quot;GamePlay/UI/Step&quot;);">
    <Label name="round-time" style="-unity-font-definition: resource(&quot;DreiFraktur&quot;); -unity-text-align: upper-center;"/>
</VisualElement>
```

### Rules
1. **Always use `resource("Path/Name")` inline in UXML** for sprites — not in USS, not `url()`. Multi-sprite PSDs under `Resources/` resolve correctly by texture name; the first/default sprite is used.
2. **Element size must match sprite native dimensions** — check the sprite rect in Inspector. Do not upscale arbitrarily; PanelSettings `Scale With Screen Size` handles display scaling.
3. **Use exact pixel values in inline styles** for pixel-art UI: `font-size: 3px`, `margin-top: 2px`, `height: 6px`. UI Builder shows these accurately when Canvas is set to art-native resolution (512×288 for menus).
4. **Match the exact font** specified by design — card names use `BITACH`, round timer uses `DreiFraktur`, general text uses `Ithaca-LVB75`.
5. **Never create wrapper elements just for background** — put `background-image` directly on the semantic root element (e.g., `round-button`, not `round-bg`).
6. **Use `-unity-text-auto-size: best-fit`** for pixel-art labels that must fit tight bounds.
7. **USS is for reusable classes and pseudo-selectors only** — per-element positioning, sizing, and sprite references belong inline in UXML.

→ [UI_MENU.md](UI_MENU.md)

---
- **Container Details:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
- **Lifetimes:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)

---

## Lesson 12: EventState.Write() in Transaction Must Snapshot Pending Events Immediately

### Mistake Made

```csharp
// WRONG — Write() only registers participant, CollectResult reads pending later
public Task Write()
{
    var handler = ...;
    handler.RecordEventStateChanged(this); // only adds participant
    return Task.CompletedTask;
}

// Later: CollectResult() calls ev.GetPendingEvents() — but Read() may have cleared them!
```

If `Read()` is called between `Write()` and `CollectResult()` (e.g. another grain method
in the same transaction), `Read()` clears `_pendingEvents` → `CollectResult()` sees empty
events → transaction commits with no events written.

### Correct Pattern

```csharp
// CORRECT — Write() passes events to handler immediately and clears local list
public Task Write()
{
    var handler = ...;
    handler.RecordEventStateChanged(this, GetPendingEvents()); // snapshot events NOW
    _pendingEvents.Clear(); // safe to clear — handler owns the snapshot
    return Task.CompletedTask;
}

// CollectResult() returns handler's accumulated records, not querying participants
```

### Rule

- **Never let transactional state hold mutable pending data across grain method boundaries.**
- `Write()` must transfer ownership of pending events to the transaction handler immediately.
- The handler must accumulate its own snapshot (`List<GrainEventRecord>`), not query
  participants during `CollectResult()`.

→ [COMMON_ORLEANS.md](COMMON_ORLEANS.md)

---

## Lesson 13: EventState.Append() Without Write() = Lost Events

### Mistake Made

```csharp
// WRONG — events appended but never committed
public async Task AddRecord(IUserRatingRecord record)
{
    await _state.Read();
    await _state.Append(new RatingAdded { Record = record });
    // Missing: await _state.Write();
    await this.SendProjection(_state.Value);
}
```

In standalone mode: events sit in `_pendingEvents` forever; grain deactivation loses them.
In transactional mode: `Append()` mutates the aggregate but `Write()` is what registers
events with the transaction handler. Without it, `CollectResult()` sees no events.

### Correct Pattern

```csharp
// CORRECT — every Append() must be followed by Write()
public async Task AddRecord(IUserRatingRecord record)
{
    await _state.Read();
    await _state.Append(new RatingAdded { Record = record });
    await _state.Write();
    await this.SendProjection(_state.Value);
}
```

### Rule

- **Every `Append()` must have a matching `Write()`** before the method returns or awaits
  an external call (grain call, DB call, delay).
- Treat `Append()` + `Write()` as an atomic pair — like `State.Write()` for direct state.

→ [COMMON_ORLEANS.md](COMMON_ORLEANS.md)

---

## Lesson 14: Missing Apply() Method = Silent Runtime/Persistence Desync

### Mistake Made

```csharp
public void Apply(SomeEvent e) => Counter += e.Amount; // exists
// No Apply(OtherEvent) — but Append silently ignores it

// Event is written to DB, aggregate in memory is NOT updated
// Next Read() (after deactivation) recalculates from events → different value!
```

### Correct Pattern

```csharp
// EventState.Append() throws if Apply method is missing
var apply = _applyCache.GetOrAdd(..., static key => {
    var applyMethod = aggType.GetMethod("Apply", ...);
    if (applyMethod == null)
        throw new InvalidOperationException($"No Apply({evtType.Name}) method found...");
    ...
});
```

### Rule

- **Every event type appended to an aggregate MUST have a matching `void Apply(TEvent)` method.**
- The exception must be thrown at `Append()` time, not at serialization or load time.
- A typo in the event type name or namespace silently breaks consistency — compile-time
  safety (source generators) is preferable to runtime reflection.

→ [COMMON_ORLEANS.md](COMMON_ORLEANS.md)
