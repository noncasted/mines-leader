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
    └─> ICard.Use(context)
            └─> CardUseResult { Result, ActionData }
                    └─> MoveSnapshot.RecordCard(actionData)
                            └─> broadcast to clients via snapshot sync
```

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
Backend action (e.g. OpenCellCommand)
    └─> MoveSnapshot.Record*(data)      # registers what changed
            └─> snapshot serialized → sent to both clients
                    └─> client SnapshotHandler.Handle(snapshot)
                            └─> update Board / Card visuals
```

### Snapshot Handlers (Client)

| Handler | Triggers on |
|---------|------------|
| `BoardSnapshotHandler` | Cell state changes (revealed, flagged) |
| `CardAddSnapshotHandler` | New card added to hand |
| `CardActionSnapshotHandler` | Card used — applies effect, then Drop animation |

**CardActionSnapshotHandler** calls `card.Drop.Enter()` after effect — this is how card is removed visually.

---

## Bot System

Three-class architecture in `backend/Game/GamePlay/Bot/`:

### BotRunner (Orchestrator)

```csharp
_round.CurrentPlayer.ViewNotNull(botLifetime, async player => {
    if (!IsBotTurn(player)) return;
    await UniTask.Delay(Random.Range(300, 500));  // thinking delay
    _ = OnBotTurnAsync(bot);                       // fire-and-forget
});
```

Turn sequence: use cards → place flags → open cells → end turn.

### BotFlagAction

Two strategies (in order):
1. **Logical**: Find Free cell where neighbor's `MinesAround > flaggedNeighborCount` → flag unflagged mine
2. **Random fallback**: Flag any discovered unflagged mine on the board

### BotCellAction

Constraint-solving:
- Find Taken cells with `MinesAround > 0`
- If all mines are flagged → open safe unflagged neighbors (`HasMine == false`)

### BotCardAction

Algorithm:
1. Filter hand cards by mana cost
2. Call `strategy.Evaluate()` for each card → get utility float (0–10)
3. Sort by utility (SortedList)
4. Execute highest utility card via `strategy.Execute()`

### IBotCardStrategy

```csharp
public interface IBotCardStrategy {
    IReadOnlyList<CardType> TargetCards { get; }   // includes _Max variants
    float Evaluate(CardType type);                  // 0-10 utility score
    Task<bool> Execute(IReadOnlyLifetime lifetime, CardType cardType);
}
```

Strategy utilities:
- **Bloodhound/ErosionDozer/ZipZap**: Utility 8 if >50% board closed, 2 otherwise
- **TrebuchetAimer**: Utility 8 if Trebuchet in hand AND opponent ahead; 1 if boost active
- **Trebuchet**: Utility 10 if boost active; 7 if opponent more open; 2 if ahead
- **OpponentBomb**: Utility 7 if opponent opened >70%
- **OpponentFlagErase**: Utility 7 if >10 flags, 5 if >5, 3 otherwise
- **GraveDigger**: Random 3–6 if stash has cards, 0 otherwise

### IBotCommandUtils

Wraps board changes in MoveSnapshot for client sync:
```csharp
WithSnapshot(Action action)                     // simple operations
WithSnapshot(Action<MoveSnapshot> action)       // operations needing snapshot reference
```

### BotBoardUtils

Stateless board analysis (injected into strategies via constructor):
- `FindRandomClosedCell()` — find unrevealed cell
- `FindClosestUnflaggedMine()` — nearest discovered mine
- `FindRandomFlaggedPosition(opponent?)` — flagged cell (own or opponent)
- `HasFlaggedCells(opponent?)` — check flag presence

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
