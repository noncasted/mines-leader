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

→ `tools/framework/.claude/docs/COMMON_CONTAINER.md`

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
| 15 | 2026-05-19 | CardIconsExporter.cs | One GetParent from Application.dataPath | Path to repo root needs two GetParent | Fixed |
| 16 | 2026-05-19 | CardIconsExporter.cs | Exporting full texture atlas + EncodeToPNG on non-readable | Load Sprite + RenderTexture + ReadPixels sprite.rect | Fixed |

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

→ [deploy-epoch.md §Фиксы пайпа](../../docs/obsidian/architecture/deploy-epoch.md)

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

→ [deploy-epoch.md §Диаграмма рестарта координатора](../../docs/obsidian/architecture/deploy-epoch.md)

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

> Obsolete (2026-11-25): the client has no UI Toolkit screens (no `.uxml` in `client/Assets`, UI is uGUI). Applies only if UI Toolkit comes back.

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

---
- **Container Details:** `tools/framework/.claude/docs/COMMON_CONTAINER.md`
- **Lifetimes:** `tools/framework/.claude/docs/COMMON_LIFETIMES.md`
- **Reactive:** `tools/framework/.claude/docs/COMMON_REACTIVE_BASICS.md`

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

→ `tools/framework/.claude/docs/COMMON_ORLEANS.md`

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

→ `tools/framework/.claude/docs/COMMON_ORLEANS.md`

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

→ `tools/framework/.claude/docs/COMMON_ORLEANS.md`

---

## Lesson 15: Unity `Application.dataPath` Points to `Assets/`, Not Repository Root

### Mistake Made
```csharp
// WRONG — one GetParent only reaches the client/ folder, not the repo root
string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
string outputDir = Path.Combine(projectRoot, "docs", "obsidian", "game", "cards", "icons");
// Result: writes to client/docs/... instead of repo-root/docs/...
```

### Correct Pattern
```csharp
// CORRECT — two GetParent calls to reach the repository root
string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName)?.FullName;
string outputDir = Path.Combine(projectRoot, "docs", "obsidian", "game", "cards", "icons");
```

### Rule
`Application.dataPath` resolves to `<repo>/client/Assets/`. One `Directory.GetParent` gives `<repo>/client/`.
Always use two `GetParent` calls to reach the repository root when writing files outside `Assets/`.

---

## Lesson 16: Exporting Unity Sprites from Atlases / PSD / Aseprite

### Mistake Made
```csharp
// WRONG — exports the entire atlas texture, not the individual sprite
Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
byte[] bytes = tex.EncodeToPNG(); // ← entire atlas, including other sprites
```

```csharp
// WRONG — EncodeToPNG throws because importer sets isReadable = 0
Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
byte[] bytes = tex.EncodeToPNG(); // UnityException: texture is not readable
```

### Correct Pattern
```csharp
// CORRECT — load Sprite, copy via RenderTexture, read only sprite.rect
Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
if (sprite == null) sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

var sourceTex = sprite.texture;
var rect = sprite.rect;

RenderTexture rt = RenderTexture.GetTemporary(sourceTex.width, sourceTex.height, 0, RenderTextureFormat.ARGB32);
Graphics.Blit(sourceTex, rt);
RenderTexture.active = rt;

Texture2D readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.ARGB32, false);
readable.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
readable.Apply();
RenderTexture.active = null;
RenderTexture.ReleaseTemporary(rt);

byte[] bytes = readable.EncodeToPNG();
```

### Rules
1. **Always load `Sprite`, not `Texture2D`** — `.psd` and `.aseprite` import as atlases with multiple sprites.
2. **Use `sprite.rect`** to read only the specific sprite region from the atlas texture.
3. **Never call `EncodeToPNG` on imported textures directly** — they are not readable. Use `RenderTexture` + `ReadPixels` to create a readable copy.
4. **For `.aseprite` files**, `LoadAssetAtPath<Sprite>` may return null; fall back to `LoadAllAssetsAtPath` and find the first `Sprite` sub-asset.

---

## Lesson 17: EventState.Append() Must Be Async — Sync Wait Deadlocks Reentrant Grains

### Mistake Made
```csharp
// WRONG — blocks the Orleans thread pool
public void Append(IEvent evt)
{
    _lock.Wait(); // sync wait inside [Reentrant] grain
    try { ... }
    finally { _lock.Release(); }
}
```

Ten concurrent standalone appends on a `[Reentrant]` grain stalled: each call held a thread while waiting for the same `SemaphoreSlim`.

### Correct Pattern
```csharp
public async Task Append(IEvent evt)
{
    await _lock.WaitAsync();
    try { ... }
    finally { _lock.Release(); }
}
```

### Rule
Never call `_lock.Wait()` (sync) on a grain hot path. `Append()` is `async Task` and uses `WaitAsync()`.

→ `tools/framework/.claude/docs/COMMON_ORLEANS.md`

---

## Lesson 18: Card Use Must Select Config by payload.Type — Never Hardcode `_Normal`

### Mistake Made
```csharp
// WRONG — Blackout_Max uses the Normal size
var config = _configs.Value.Blackout_Normal;
```

Menu preview of Blackout and BlackoutMax looked identical. Same bug still exists in `Smoke.cs`, `Frost.cs`, `FogOfWar.cs`.

### Correct Pattern
```csharp
var config = payload.Type == CardType.Blackout_Max
    ? _configs.Value.Blackout_Max
    : _configs.Value.Blackout_Normal;
```

### Rule
Every card with a `_Max` variant must branch on `payload.Type`. Do not copy `_configs.Value.X_Normal` as a default.

→ [CARD_EFFECTS.md](CARD_EFFECTS.md)

---

## Lesson 19: [Inject] Method Must Be Public When the Scope Lives in Another Assembly

### Mistake Made
```csharp
// tools/framework/client/modules/audio (assembly Internal.Audio), registered from Global.Setup (assembly Global)
[Inject]
internal void Construct(IAudioVolume volume, IUpdater updater)
```

Roslyn does not import internal members of referenced assemblies, so ContainerGenerator does not see the method,
emits no `Construct` call and reports nothing. The fields stay null; the WebGL build crashed with
`MethodAccessException: Attempt to access method 'Global.Audio.IAudioVolume.get_MasterVolume' on type ''`
(IL2CPP interface call on null). Same for `GlobalCamera` and `LoadingScreen` after the move to the framework.

### Correct Pattern
```csharp
[Inject]
public void Construct(IAudioVolume volume, IUpdater updater)
```

### Rule
`internal` `[Inject]` is fine only when the type is registered from its own assembly. Any type in a framework module
(or any assembly other than the scope's) needs a `public` `[Inject]` method. After moving a type between assemblies,
check that the generated container calls its `Construct`.

---

## Lesson 20: Session Container Has No AddOptions() — Register IOptions<T> Explicitly

### Mistake Made
The old game `AddSessionServices` called `services.AddLogging(...)`, which calls `AddOptions()` and so made every
`IOptions<T>` resolvable with a default `T`. The framework session passes the host `ILoggerFactory` instead, so after
the move `IOptions<BoardOptions>` in `PlayerFactory` failed on prod: `RequestWithBot` was rejected and no match started.

### Correct Pattern
```csharp
services.AddSingleton(Options.Create(new BoardOptions()));
```

### Rule
Never rely on `IOptions<T>` coming from `AddLogging`/`AddOptions` side effects in a session container: register each
options instance explicitly. When changing session registrations, build the match container with
`new ServiceProviderOptions { ValidateOnBuild = true }` for every match type (bot and human) to see all gaps at once.

---

## Lesson 21: Regenerate All on Broken Compilation Deletes Content Group Assets

### Mistake Made
During the move to framework 0.8.0 (pointer moved, Addressables removed from the manifest, game code not yet compiling)
Regenerate All was run. It deleted the content group assets of the game (generated from types that were not compiled);
they had to be brought back from git.

### Correct Pattern
```text
1. Bring the code to compile (Unity console clean, dotnet build client.sln 0 errors).
2. Only then: Regenerate All.
3. git status - no deleted group/catalogue assets you did not intend.
```

### Rule
Never run Regenerate All (or Project Tools, Run All) while the project has compile errors. After every Regenerate All on a
framework update, check `git status` for deleted assets before going on.

---

## Lesson 22: `git -C tools/framework` Without a Cloned Submodule Runs on the Parent Repository

### Mistake Made
A worktree of the game was created and the submodule clone failed (`transport 'file' not allowed`). The next
`git -C tools/framework checkout <framework commit>` ran on the game worktree itself (the empty folder is not a repository,
git walks up to the parent): the framework commit was checked out into the game worktree and the framework tags were fetched
into the game repository.

### Correct Pattern
```bash
git -c protocol.file.allow=always submodule update --init tools/framework
test "$(git -C tools/framework rev-parse --show-toplevel)" = "$PWD/tools/framework" || exit 1
git -C tools/framework checkout <commit>
```

### Rule
Before any `git -C <submodule>` command in a fresh clone, copy or worktree, check that `rev-parse --show-toplevel` is the
submodule path itself. A failed submodule clone must stop the script, not fall through.

## Lesson 23: A Pooled View Keeps the State of Its Last Show

### Mistake Made
`GameFloatingTextView.Setup` set the text colour only when the request had one and then read `_text.color` back as the base
of the fade. With a fresh `Instantiate` per text that was the prefab colour; with `PrefabPool<T>` it is the faded colour of
the previous show (alpha 0), so the second text without a colour is invisible.

### Correct Pattern
Capture the prefab values once (`Awake`) and set every field the view changes on each show:
`_textColor = request.TextColor ?? _defaultTextColor; _text.color = _textColor;`.

### Rule
A view taken from `PrefabPool<T>` resets on every take everything it changes during a show (colours, alpha, scale,
position, active children, sprites). "If the request has X, set X" is a bug for a pooled view.

## Lesson 24: Code Comments Written in Russian Because the Answers Are in Russian

### Mistake Made
The user asks for prose of answers in Russian, and the agent carried the language into the code: `//` and `///` comments in
Russian, next to English ones in the same file (about 300 lines across client, backend and shared had to be translated).

### Correct Pattern
```csharp
// The flag is set after the value: whoever waited for readiness already sees the data.
```

### Rule
Prose of answers and docs of the game is Russian; identifiers and comments in code are English. A comment explains "why"
and is written in English from the start. String literals with UI texts are content and stay as they are.
`/validate` and the analyzer FWR004 report Cyrillic in comments.

## Lesson 25: A Web SDK exe Turned into a Library Keeps Exe-Only Assumptions

### Mistake Made
The role projects (`Orchestration/<Role>`) became class libraries so a monolith could run their `Build(WebApplicationOptions)`
factories, and the build failed: `Routes.razor` had `typeof(Program).Assembly` — there is no `Program` in a library (CS0246).
Two more things would have failed silently: the card icons `Content` item stayed in the library and never reached the exe's
publish output (Content does not flow through a `ProjectReference`), and the appsettings moved to the library folder while the
content root is the exe's folder.

### Correct Pattern
```razor
<Router AppAssembly="@typeof(App).Assembly" ...>
```

### Rule
When a `Microsoft.NET.Sdk.Web` exe becomes a class library, go through everything that belonged to the exe and move it:
`typeof(Program)` (use the app component or the host class), `AppContext.BaseDirectory` paths, `Content` /
`CopyToPublishDirectory` items (duplicate into every exe that publishes), `appsettings*.json` and `Properties/launchSettings.json`
(the content root is the exe folder), and the static web assets manifest (`<ApplicationName>.staticwebassets.endpoints.json`
only exists in the exe — a monolith names the console host after itself).
