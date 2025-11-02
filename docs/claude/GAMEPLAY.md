# Client GamePlay Module

Comprehensive gameplay implementation including card mechanics, player state, game flow orchestration, and server synchronization.

## Module Structure

```
GamePlay/
├── Boards/         - Grid system and cell management
├── Cards/          - Card entities, deck, hand, stash
├── Players/        - Player entities and stats
├── Loop/           - Game flow orchestration
├── Services/       - Core gameplay services
├── Sync/           - Server synchronization handlers
├── UI/             - Game interface
└── Cheats/         - Development utilities
```

---

## Core Game Systems

### 1. Player System (25 C# files)

**Purpose:** Player entities with health, mana, moves, deck, and hand.

**Key Components:**

| Component | Purpose |
|-----------|---------|
| `IGamePlayer` | Complete player entity aggregate |
| `IPlayerHealth` | Health reactive property (current/max) |
| `IPlayerMana` | Mana reactive property (current/max) |
| `IPlayerMoves` | Moves/turn counter |
| `IDeck` | Remaining cards to draw |
| `IHand` | Cards in hand with positions |
| `IStash` | Special card collection |
| `IBoard` | 9x9 grid (9 for each player) |

**Player Aggregate Structure:**

```
GamePlayer
├── IPlayerHealth (reactive)
├── IPlayerMana (reactive)
├── IPlayerMoves (reactive, current/max)
├── IDeck (remaining cards)
├── IHand (played cards)
├── IStash (special cards)
├── IBoard (player's grid)
└── Modifiers (status effects)
```

**State Synchronization:**

Each component has `IViewableProperty<State>`:
- Views sync through `IScopeLoaded.OnLoaded()`
- Updates trigger through reactive subscriptions
- Server changes broadcast via NetworkProperty

### 2. Game Round System

**Purpose:** Track turns, round time, and turn allowance.

**Key Components:**

- `IGameRound` - Round state and control
- `GameRoundState` - Shared with server
- `IsTurnAllowed` - Check if local player's turn
- `RoundTime` - Seconds remaining

### 3. Game State System

**Purpose:** Match lifecycle (waiting, active, completed).

**States:**

```
WaitingForPlayers
  └─ Network connection, opponent search

Active
  └─ Gameplay in progress

Completed
  └─ Match finished, winner determined
```

---

## Server Synchronization

### Snapshot-Based System

```
Server (Orleans)
  ↓
SharedMoveSnapshot (DTO with IMoveSnapshotRecord[])
  ↓
SnapshotReceiver (queues records)
  ↓
Type-based handler dispatch (async)
  ↓
Record-specific mutations
```

### Synchronization Handlers

**Core Handlers:**

All snapshot handlers are type-based:

| Handler Type | Records Handled |
|---------|-----------------|
| Board-related | Board state changes (cells, flags) |
| Card-related | Card actions, draw, remove, stash |
| Player-related | Health, mana, moves updates |

For detailed handler information, see **docs/GAMEPLAY_CARDS.md**

**Handler Registration:**

```csharp
builder.Register<SnapshotReceiver>();
builder.Add(typeof(SharedBoardSnapshot), BoardSnapshotHandler.Handle);
builder.Add(typeof(PlayerSnapshotRecord.Card), CardActionSnapshotHandler.Handle);
// ... more handlers
```

### Input-to-Server Flow

```
User Action (cell click, card use)
  ↓
CellOpenAction / CardUseAction validation
  ↓
Local State Update (optimistic)
  ↓
Server Request (SharedGameAction)
  ↓
Server validates and broadcasts
  ↓
SharedMoveSnapshot to all clients
  ↓
SnapshotHandler applies mutations
```

---

## Game Context

**Purpose:** Central hub accessing all game data and state.

**Provides:**

```csharp
IGameContext
├── IReadOnlyList<IGamePlayer> Self / Other / All
├── IGamePlayer GetPlayer(Guid userId)
├── IGameState Current state
├── IGameRound Current round info
└── Options Game settings
```

---

## Game Loop Orchestration

**File:** `Loop/PvP/PvPGameLoop.cs`

**Three Phases:**

1. **WaitingForPlayers**
   - Network connection establishment
   - Wait for opponent to connect

2. **Active**
   - Initialize gameplay services
   - Send PlayerReady command
   - Execute game loop

3. **Completed**
   - Determine winner
   - Process end-game UI
   - Return to menu or rematch

### Event System

**MatchEventLoop** - Reactive state listener:

```csharp
_state.Value.View(lifetime, (stateLifetime, state) =>
{
    switch (state)
    {
        case GameStateType.Active:
            OnMatchStarted(stateLifetime);
            break;
        case GameStateType.Completed:
            OnMatchCompleted(stateLifetime, data);
            break;
    }
});
```

---

## Key Interfaces

### Players
```csharp
IGamePlayer, IGamePlayerInfo, IPlayerHealth, IPlayerMana, IPlayerMoves, IGamePlayerFactory
```

### Game Flow
```csharp
IGameContext, IGameState, IGameRound, IPvPGameLoop, ISnapshotReceiver, ISnapshotHandler<T>
```

### Services
```csharp
IGameInput, IGameCamera, ISnapshotReceiver
```

---

## Dependency Injection

**Main Extension:**

```csharp
builder
    .AddDefaultGamePlayServices()  // Board, Players, Cards
    .AddGameEndServices()          // End-game UI
    .Register<PvPGameLoop>().As<IPvPGameLoop>()
    .AddNetworkService<GameState>()
    .Register<MatchEventLoop>().As<IScopeSetup>();
```

**Scene Loading:**

```csharp
await UniTask.WhenAll(
    builder.FindOrLoadSceneWithServices<GameFieldScene>(),
    builder.FindOrLoadSceneWithServices<GameOverlayScene>(),
    builder.FindOrLoadSceneWithServices<GamePauseScene>(),
    builder.FindOrLoadSceneWithServices<GameEndScene>(),
    builder.FindOrLoadSceneWithServices<GameCheatsScene>()
);
```

---

## Lifetime Management

**Scope Hierarchy:**

```
Global Scope (parent)
  ↓
GamePlay Scope (game session)
  ├── GameContext lifetime
  ├── Board lifetime
  ├── Player lifetimes
  ├── Card lifetimes
  └── Automatic cleanup on scope disposal
```

---

## Critical Patterns

### Pattern 1: NetworkProperty Reactivity

```csharp
NetworkProperty<PlayerState> _state;
  ↓
ViewableProperty<bool> _isDirty;
  ↓
UI/Logic subscriptions via Advise(lifetime, handler)
```

### Pattern 2: Snapshot Processing

```csharp
SnapshotReceiver receives SharedMoveSnapshot
  ├─ Enqueues IMoveSnapshotRecords
  ├─ Dispatches by type
  ├─ Handler executes mutations
  └─ Async processing via UniTask
```

### Pattern 3: Player State Synchronization

```
Server updates player state
  ↓
SharedPlayerSnapshot received
  ↓
Handler applies mutations
  ↓
ViewableProperty updates
  ↓
UI reflects changes
```

For card-specific patterns, see **docs/GAMEPLAY_CARDS.md**

---

## Logging Tags

```
[Gameplay] [Session]     - Session lifecycle
[Gameplay] [Commands]    - Command processing
[Gameplay] [Objects]     - Entity management
[Gameplay] [Players]     - Player state updates
[Gameplay] [GameLoop]    - Game loop events
```

For board-specific logging, see **docs/GAMEPLAY_BOARD.md**
For card-specific logging, see **docs/GAMEPLAY_CARDS.md**

---

## Assembly Dependencies

**Depends on:**
- Common (Network, Animation)
- Internal (Lifetime, DI, Reactive)
- Global (Camera, Input, UI, Updater)
- Meta (User, Matchmaking, Backend)
- Shared (Game domain models)

**Used by:**
- Loop (Game loop orchestration)

---

## Key Files

| File | Purpose |
|------|---------|
| `Loop/PvP/PvPGameLoop.cs` | Main game orchestrator |
| `Loop/Context/GameContext.cs` | Player and state hub |
| `Players/Entity/Root/GamePlayer.cs` | Player aggregate |
| `Services/SnapshotReceiver.cs` | Snapshot queue processor |
| `Sync/PlayerSnapshotHandler.cs` | Player state sync |

For board files, see **docs/GAMEPLAY_BOARD.md**
For card files, see **docs/GAMEPLAY_CARDS.md**

---

## See Also

- **docs/GAMEPLAY_BOARD.md** - Board and cell system
- **docs/GAMEPLAY_CARDS.md** - Card system and card context
- **docs/NETWORK.md** - Network synchronization
- **docs/LOOP.md** - Game flow control
- **docs/META.md** - Backend integration
- **docs/COMMON_REACTIVE.md** - Reactive patterns
