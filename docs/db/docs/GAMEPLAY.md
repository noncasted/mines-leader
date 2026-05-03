# Gameplay Architecture: Mines Leader

Reference for game flow, board system, card system, snapshot sync, and bot AI.

## Quick Navigation

| Looking for... | Go to section |
|----------------|---------------|
| Game flow (who calls what) | Game Flow |
| Board, cells, mine/flag state | Board System |
| ICard interface, all card types | Card System |
| Client↔backend sync protocol | Snapshot Sync |
| Bot AI, card strategies | Bot System |
| Key file paths | Key Files |

---

## Project Layout

```
mines-leader/
├── client/          # Unity3D (VContainer DI, UniTask, reactive/lifetime framework)
│   └── Assets/
│       ├── GamePlay/    # Boards, Cards, Players, Sync, UI, Loop
│       ├── Meta/        # Matchmaking, Auth, Deck management
│       ├── Menu/        # Main menu, deck editor
│       └── Common/      # Shared utilities, animations
├── backend/         # .NET Orleans (distributed actor model)
│   ├── Game/        # Core game logic (Orleans grains)
│   ├── Meta/        # Users, Matches, Matchmaking, Bots
│   ├── Infrastructure/  # Persistence, messaging, execution loop
│   └── Orchestration/   # HTTP gateways (GameGateway, MetaGateway), Console UI
└── shared/          # Protocol, domain models, configs (used by both sides)
    ├── Game/        # GameState, BoardState, CellState, MoveSnapshot
    ├── Domain/      # CardType, GameMatchType enums
    ├── Protocol/    # Network message definitions
    └── Configs/     # CardConfigOptions, GameModeOptions, BotConfigOptions
```

---

## Game Flow

```
GameFlow.Process()
    └─> GameRound.Process()              # loops turns until winner found
            └─> RoundActionService       # executes one player's turn
                    ├─> OpenCellCommand      # player opens cell
                    ├─> CardUseCommand       # player uses card
                    ├─> PlayerReadyCommand   # player signals end of turn
                    └─> MoveSnapshot         # records all board changes for sync
```

**GameFlow** (`backend/Game/GamePlay/Context/GameFlow.cs`):
- Orchestrates entire match: Setup → rounds loop → rematch check
- Holds references to both players' Board grains

**GameRound** (`backend/Game/GamePlay/Context/Rounds/`):
- `TimeLimitedRound` — round ends after time limit
- `LastManStandingRound` — elimination round

**RoundActionService** (`backend/Game/GamePlay/Context/RoundActionService.cs`):
- Executes single player's turn actions
- Validates commands, applies to board, records in MoveSnapshot

---

## Board System

### Cell States

```
CellState:
  Free     — not yet revealed (default; contains mine or safe)
  Taken    — revealed safe cell (HasMine=false, MinesAround=0..8)
```

**Key cell fields** (`shared/Game/` + `backend/Game/GamePlay/Board/`):
- `HasMine` — whether the cell contains a mine
- `MinesAround` — count of adjacent mines (0–8)
- `IsFlagged` — player flagged this cell as suspected mine
- `Position` — (x, y) on the board grid

### Board Operations

| Operation | Class | Notes |
|-----------|-------|-------|
| Generate mines | `BoardGenerator` | Procedural placement, seeds per match |
| Scan neighbors | `BoardMinesScanner` | Fills `MinesAround` for all cells |
| Reveal cell | `BoardRevealer` | Flood-fill for safe zones |
| Flag cell | `RemoveFlagAction` / `CellFlagAction` | Toggle flag |
| Open cell | `OpenCellCommand` | Validates → reveals → checks win |

### BoardRevealer Flood-Fill (Corrected Algorithm)

When a safe cell (MinesAround=0) is opened, adjacent safe cells auto-reveal:

```
For each Taken neighbor of target cell:
    Check if that Taken neighbor has Free neighbors with MinesAround == 0
    If yes → add target to flood fill → recurse
```

**Critical:** Check is based on neighbors' MinesAround, NOT on HasMine of the cell itself.
Wrong approach: expanding from current cell checking if neighbors have mines.
Correct approach: reverse neighbor check — find Taken cells whose Free neighbors have 0 mines around.

---

## Card System

### ICard Interface

```csharp
public interface ICard {
    CardType Type { get; }
    UniTask<CardUseResult> Use(IGameContext context);
}

public class CardUseResult {
    public required EmptyResponse Result { get; init; }
    public required ICardActionData? ActionData { get; init; }
}
```

`ActionData` — data passed to client snapshot handlers to play visual effects.

### Card Design Documentation

Card ideas, statuses, and validation rules are in `docs/obsidian/game/cards/`:
- `implemented/` — cards currently in the game (26 cards)
- `queue/` — approved for implementation (30 cards)
- `ideas/` — in concept stage (12 cards)
- `fail/` — rejected cards (80+) with `fail_reasons.md` — **mandatory validation checklist for new card ideas**

When designing a new card, always validate against `fail/fail_reasons.md` first.

### CardType Enum (all values in `shared/Domain/`)

| Card | Effect |
|------|--------|
| `Bloodhound` | Reveals mines in an area (does not open cells) |
| `ErosionDozer` | Damages opponent's board — auto-opens cells |
| `ZipZap` | Opens multiple safe cells on own board |
| `Trebuchet` | Direct damage to opponent (opens cells) |
| `TrebuchetAimer` | Boosts next Trebuchet if opponent is ahead |
| `OpponentBomb` | Forces opponent to open cells in an area |
| `OpponentFlagErase` | Removes flags from opponent's board |
| `OpponentFlagReshuffle` | Reshuffles flag positions on opponent's board |
| `GraveDigger` | Draws cards from discard stash |
| `Smoke` | Conceals revealed cells on opponent's board |

Each card type also has a `_Max` variant (upgraded version with enhanced effect).

### Card Lifecycle (Backend)

```
CardUseCommand.Execute()
    prefixMark = snapshot.Count                           # remember insertion point
    ├─> ICard.Use(context)                                # card mutates state + writes records,
    │       └─> CardUseResult { Result }                  # including its own snapshot.RecordCardUse(...)
    ├─> using (snapshot.BeginInsertAt(prefixMark)) {
    │       Hand.Remove + RecordCardRemove                # all inserted BEFORE the card's records
    │       Mana.Use                                      # (auto-records ManaUpdate)
    │       Moves.OnUsed                                  # (auto-records MovesUpdate)
    │   }
    └─> Stash.Add + RecordCardAdd(isStash: true)          # appended AFTER card's records
```

**Final record order in snapshot**:
`CardRemove → ManaUpdate → MovesUpdate → [card's side-effect records incl. RecordCardUse] → CardAdd(stash)`.

This ordering guarantees the client-side playback matches visual choreography: card leaves hand → resources spent → card animation → card lands in stash.

`CardUseResult` carries only `EmptyResponse Result`. Cards that need an `ICardActionData` payload call `context.Snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.X(...))` **inside** `Use()`. Cards must validate before any mutation — if they return `Fail`, the snapshot must remain empty of side-effects.

**Reveal cards** (Bloodhound, ChaosDiamond, ChaosScout, ErosionDozer, Excavator, ZipZap, MinefieldScout, OpponentBomb) call `board.Revealer.Reveal(positions)` directly — the reveal is **not** recorded as `CellFree`/`MinesAround`, and the card does **not** call `snapshot.RecordMines(...)`. The opened cells travel to the client through two parallel fields on `CardActionSnapshot.X`: `OpenedCells` — strictly cells that became Free (including former mine tiles the card cleared), used by `CardActionSnapshotHandler.PlayActionAnimation` for `PlayCellAction`; `UpdatedFreeCells` — `OpenedCells` ∪ free neighbours whose `MinesAround` changed, used by `ICardActionSync` to call `cell.EnsureFree().OnMinesUpdated(opened.MinesAround)`. For cards without neighbour side-effects (OpponentBomb, MinefieldScout, Excavator, ChaosDiamond, ChaosScout) `UpdatedFreeCells = OpenedCells`. Only Bloodhound / ErosionDozer / ZipZap add `board.GetFreeNeighbours(...)` into `UpdatedFreeCells`. `SnapshotApplier.ApplyCardUseReveals` reads `data.UpdatedFreeCells ?? data.OpenedCells` (fallback for older snapshots) to mirror the write for diff-guard validation.

### Card Lifecycle (Client)

```
CardAddSnapshotHandler    → CardFactory.Create(lifetime, isLocal, cardId, type)
CardActionSnapshotHandler → card.Use() → card.Drop.Enter()   # cleanup animation
```

Client card classes:
- `LocalCard` — own cards, drag+drop interaction
- `RemoteCard` — opponent's cards, display only
- `CardLocalDrag` / `CardLocalDrop` — drag and play animation
- `CardZipZapAction`, `CardBloodhoundAction`, etc. — visual effect per card type

### Card ID System

Cards use `Guid Id` (not integer EntityId). `CardFactory.Create(lifetime, isLocal, cardId, type)`.

---

## Snapshot Sync

MoveSnapshot is the client-server sync protocol for board state changes.

### How It Works

```
Backend action (e.g. OpenCellCommand, Card.Use, round start/end)
    └─> MoveSnapshot.Record*(data)         # explicit, per-mutation write
            └─> (optional) SnapshotDiffGuard.Validate(pre, records, post)
            └─> snapshot serialized → sent to both clients
                    └─> client SnapshotHandler.Handle(snapshot)
                            └─> update Board / Card visuals
```

**Manual-only API.** `MoveSnapshot` has no implicit subscription to engine events —
every cell/mana/health/moves/modifier mutation must be paired with a matching
`snapshot.Record*` call. The record order is the client playback order — cards
use this to choreograph animations (flash → explosion → cell open → mana refill).

### Record API

| Method | What it records |
|--------|----------------|
| `RecordCellTaken(board, pos)` / `RecordCellFree(board, pos)` | Cell status change (resets flag/effects/mines) |
| `RecordFlag(board, pos, isFlagged)` | Flag toggle |
| `RecordMines(board, pos, count)` | `MinesAround` recalculation |
| `RecordExplosion(board, pos)` | Visual-only (no state change) |
| `RecordEffectAdded/Removed(board, pos, ...)` | Cell effect add/remove |
| `RecordManaUpdate(player)` / `RecordHealthUpdate` / `RecordMovesUpdate` | Resource change |
| `RecordModifierUpdate(player, modifier, value)` | Modifier change — **required** when a modifier affects derived `Max` |
| `RecordCardAdd(playerId, cardId, type, isStash)` | Card lands in hand (default) or stash (`isStash: true`) |
| `RecordCardRemove / RecordCardUse(...)` | Card leaves hand / card-use notification with `ICardActionData` |
| `BeginInsertAt(index)` *(IDisposable)* | Redirects subsequent `Record*` calls to insert at `index` instead of appending — used by `CardUseCommand` to place CardRemove/Mana/Moves before the card's own records |
| `RecordReveal(board, positions)` *(extension)* | **Non-card sites only** (OpenCellCommand, BotCellAction): reveal + `CellFree` + `MinesAround` records. Cards call `board.Revealer.Reveal` without recording. |

### SnapshotDiffGuard (dev/test)

`SnapshotDiffGuard` captures game state **before** each snapshot block, applies the
collected records to that snapshot via `SnapshotApplier`, and compares the result
against the **actual** post-mutation state. On divergence it throws
`SnapshotDiffException` with a per-field diff — "Player X Mana: expected 3/5, got 7/5" —
pointing directly at the card/command that forgot a `Record*` call.

- Toggle from the Console (Features page) via `ClusterFeaturesState.SnapshotDiffGuardEnabled`,
  exposed on `IClusterFlags.SnapshotDiffGuardEnabled`.
- Guard call sites: `GameCommand.Execute`, `BotCommandUtils.WithSnapshot`,
  round snapshot blocks (init / round-start / round-end).
- Enabled by default in the Orleans test cluster.

### Snapshot Handlers (Client)

| Handler | Triggers on |
|---------|------------|
| `BoardSnapshotHandler` | Cell state changes (revealed, flagged) |
| `CardAddSnapshotHandler` | New card added to hand |
| `CardActionSnapshotHandler` | Card used — applies effect, then Drop animation |
| `PlayerModifierSnapshotHandler` | Modifier update (currently no-op, required for MemoryPack union dispatch) |

**CardActionSnapshotHandler** calls `card.Drop.Enter()` after effect — this is how card is removed visually.

---

## Bot System

Architecture in `backend/Game/GamePlay/Bot/`. Config: `BotConfigOptions` (shared).

### BotRunner (Orchestrator)

```csharp
_round.CurrentPlayer.ViewNotNull(botLifetime, (roundLifetime, player) => {
    if (player.User.Id != user.Id) return;
    if (player.Moves.IsAvailable == false) return; // guard against init trigger
    Task.Run(() => OnBotTurn(roundLifetime));
});
```

Turn sequence: **interleaved** — card → flag → cell → card → flag → cell → ...
- Random round duration: `MinRoundTime`–`MaxRoundTime` (default 7–12s)
- Random delay 0.5–2s between actions (distributed across time budget)
- Waits remaining budget before ending turn (human-like pacing)

### BotFlagAction

Constraint-solving: Find Free cell where `MinesAround > 0`, check Taken neighbors for unflagged mines.
Logs decision reason: which constraint triggered the flag.

### BotCellAction

Two strategies (in order):
1. **Constraint-solving**: Free cell with flagged neighbors → open safe unflagged neighbor (`HasMine == false`)
2. **Fallback** (HP > 1 only): Open random Taken cell known to have no mine

### BotCardAction

Algorithm:
1. Filter hand cards by mana cost
2. Call `strategy.Evaluate()` → get base utility (0–10)
3. Add mana-efficiency bonus: `(1 - manaCost/6) * 1.5` — cheaper cards score higher
4. Try cards in descending utility order
5. If Execute() fails → try next candidate (not give up)

### IBotCardStrategy

```csharp
public interface IBotCardStrategy {
    IReadOnlyList<CardType> TargetCards { get; }   // includes _Max variants
    float Evaluate(CardType type);                  // 0-10 utility score
    bool Execute(Guid cardId, CardType cardType);
}
```

Strategy utilities (gradient, not binary):
- **Bloodhound**: `3 + closedRatio * 7.5` — cheap (2 mana), good info, prioritized
- **ErosionDozer**: `2 + closedRatio * 8` — slightly lower than Bloodhound, more expensive
- **ZipZap**: `4 + closedRatio * 6.5` — destroys mines, always high if target exists. Returns 0 if no valid target (HasValidTarget check). Passes MINE position to card.
- **TrebuchetAimer**: 8 if Trebuchet in hand AND opponent ahead; 1 if boost active
- **Trebuchet**: 10 if boost active; 7 if opponent more open; 2 if ahead
- **OpponentBomb**: 7 if opponent opened >70%. 0 if opponent HP <= 1 (no finishing). Searches OPPONENT's board.
- **OpponentFlagErase**: 7 if >10 flags, 5 if >5, 3 otherwise
- **Smoke**: 6 if opponent openRatio > 60%, 4 if > 30%, 2 otherwise
- **Gravedigger**: `min(3 + stash*2 + handBonus, 9)` — scales with stash size

### IBotCommandUtils

Wraps board changes in MoveSnapshot for client sync:
```csharp
WithSnapshot(Action action)                     // simple operations
WithSnapshot(Action<MoveSnapshot> action)       // operations needing snapshot reference
bool UseCard(IPlayer bot, Guid cardId, ICardUsePayload payload)  // execute card via service provider
```

### BotBoardUtils

Stateless board analysis (injected into strategies via constructor):
- `FindRandomClosedCell()` — find unrevealed cell
- `FindClosestUnflaggedMine()` — nearest discovered mine
- `FindRandomFlaggedPosition(opponent?)` — flagged cell (own or opponent)
- `HasFlaggedCells(opponent?)` — check flag presence

### Session Logging

`ISessionLogger` logs all bot decisions to `backend/.telemetry/logs-games/{date}/{sessionId}.log`:
- Player labels: `Human`/`Bot` instead of GUIDs (via `RegisterPlayers`)
- Round separators: `-------- Round N Start/End PLAYER --------`
- Bot state at turn start: mana, moves, hand, health
- Card evaluation: all candidates with utility, skipped cards with reasons (no mana, zero utility, no strategy)
- Flag/cell decisions: position + constraint-solve reason
- Mana changes at round end

---

## Matchmaking Flow

```
Client: SearchGame() or CreateGameWithBot()
    └─> MetaGateway/Matchmaking.cs
            ├─> Add to search queue with joinedAt timestamp
            ├─> Loop() checks wait time vs BotConfigOptions.MatchmakingApplyThreshold (default 20s)
            └─> If wait exceeded AND user connected → MatchFactory.CreateWithBot(userId, type)

MatchFactory.CreateWithBot():
    └─> Pick random bot from BotCollection (AddressableDictionary)
    └─> Fallback to Guid.Empty + warning if no bots available
```

---

## Testing Conventions

### BoardParser — visual board layout for all gameplay tests

ALL gameplay tests MUST use `BoardParser` for boards — never `TestBoardBuilder` directly.

```csharp
// 1. Create initial board from ASCII
var board = BoardParser.Parse("""
    . . . .
    . ? . .
    . . M .
    . . . .
""");

// 2. Perform action
card.Use(...);

// 3. Verify result via ASCII diff
BoardParser.AssertBoard(board, """
    . . . .
    . 1 . .
    . . X .
    . . . .
""");
```

**Why:** programmatic board construction hides what the board actually looks like. `BoardParser` gives a visual, readable representation matching the real game. Reference tests: `OpponentBombTests.cs`, `BloodhoundTests.cs`.

`TestBoardBuilder` is internal — used only inside `BoardParser`, never in test methods directly.

---

## Key Files

### Backend

| File | Purpose |
|------|---------|
| `backend/Game/GamePlay/Context/GameFlow.cs` | Match orchestrator |
| `backend/Game/GamePlay/Context/RoundActionService.cs` | Turn execution |
| `backend/Game/GamePlay/Board/Board.cs` | Board state + events |
| `backend/Game/GamePlay/Board/BoardRevealer.cs` | Flood-fill reveal algorithm |
| `backend/Game/GamePlay/Cards/ICard.cs` | Card interface |
| `backend/Game/GamePlay/Cards/CardFactory.cs` | Creates card instances |
| `backend/Game/GamePlay/Commands/OpenCellCommand.cs` | Open cell command |
| `backend/Game/GamePlay/Commands/CardUseCommand.cs` | Card use command |
| `backend/Game/GamePlay/Bot/BotRunner.cs` | Bot orchestrator |
| `backend/Game/GamePlay/Bot/BotCardStrategies.cs` | Strategy factory (CardType → IBotCardStrategy) |
| `backend/Meta/Bots/BotCollection.cs` | Bot user registry (Orleans grain) |
| `backend/Meta/Matches/MatchFactory.cs` | Creates matches, assigns bots |
| `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs` | Queue + timeout logic |

### Client

| File | Purpose |
|------|---------|
| `client/Assets/GamePlay/Loop/Context/GameContext.cs` | Client game session context |
| `client/Assets/GamePlay/Loop/Context/MatchEventLoop.cs` | Listens for match updates |
| `client/Assets/GamePlay/Boards/Root/Board.cs` | Client board representation |
| `client/Assets/GamePlay/Boards/Cells/CellView.cs` | Cell visual component |
| `client/Assets/GamePlay/Boards/Cells/CellAnimator.cs` | Cell reveal/flag animations |
| `client/Assets/GamePlay/Cards/Services/Factory/CardFactory.cs` | Creates card visuals |
| `client/Assets/GamePlay/Cards/Entities/View/CardView.cs` | Card visual component |
| `client/Assets/GamePlay/Sync/BoardSnapshotHandler.cs` | Syncs board state |
| `client/Assets/GamePlay/Sync/CardAddSnapshotHandler.cs` | Creates cards from snapshot |
| `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` | Applies card effects |
| `client/Assets/Meta/Matchmaking/Matchmaking.cs` | Client matchmaking service |

### Shared

| File | Purpose |
|------|---------|
| `shared/Domain/CardType.cs` | CardType enum (all card values + _Max) |
| `shared/Domain/GameMatchType.cs` | Match type enum |
| `shared/Game/MoveSnapshot.cs` | Sync protocol for board changes |
| `shared/Configs/BotConfigOptions.cs` | Bot behavior thresholds |
| `shared/Protocol/` | Network message definitions |
