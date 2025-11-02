# Client Loop Module

Game flow orchestration coordinating Menu ↔ GamePlay transitions and state machine.

## Module Structure

```
Loop/
├── Loaders/         - Scope loading orchestration
├── Setup/           - Dependency injection configuration
└── Root files       - GameLoop state machine
```

---

## Main Components

### GameLoop

**File:** `GameLoop.cs`

Main state machine using nested async functions pattern:

```csharp
public class GameLoop : IScopeLoaded
{
    public void OnLoaded(IReadOnlyLifetime lifetime)
    {
        Loop(lifetime).Forget();
    }

    private UniTask Loop(IReadOnlyLifetime lifetime)
    {
        Menu().Forget();
        return UniTask.CompletedTask;

        async UniTask Menu()
        {
            var menuResult = await _menuLoader.Load();
            Game(menuResult).Forget();
        }

        async UniTask Game(GameLoadData loadData)
        {
            var transition = await _gamePlayLoader.Load(loadData);

            switch (transition)
            {
                case GameEndTransition.Exit exit:
                    Menu().Forget();
                    break;
                case GameEndTransition.Rematch rematch:
                    Game(new GameLoadData()
                    {
                        GameMode = loadData.GameMode,
                        SessionData = rematch.NewSession
                    }).Forget();
                    break;
            }
        }
    }
}
```

**Pattern Characteristics:**

- **Local Functions** - Menu and Game are nested functions
- **Fire-and-Forget** - `.Forget()` signals non-awaited UniTask
- **Recursive Flow** - Each state spawns the next state
- **No State Enum** - Uses function calls instead of switch
- **Type-Safe Transitions** - IGameEndTransition discriminated union

---

## Scope Management

### GameLoopScopeLoader

**File:** `Loaders/GameLoopScopeLoader.cs`

Manages single active scope lifecycle:

```csharp
public class GameLoopScopeLoader : IGameLoopScopeLoader
{
    public async UniTask<ILoadedScope> Load(
        Func<IServiceScopeLoader, ILoadedScope, UniTask<ILoadedScope>> action)
    {
        _currentScope?.Dispose().Forget();  // Cleanup previous
        var currentScope = await action(_serviceScopeLoader, _parentScope);
        _currentScope = currentScope;
        return _currentScope;
    }
}
```

**Key Features:**

- Manages only ONE active scope at time
- Automatically disposes previous scope
- Prevents memory leaks during transitions
- Parent scope reference for scope hierarchy

---

## State Flow

### MenuLoader

**File:** `Loaders/MenuLoader.cs`

Loads Menu scope and waits for game selection:

```csharp
public class MenuLoader : IMenuLoader
{
    public async UniTask<GameLoadData> Load()
    {
        _globalCamera.Enable();
        await _loadingScreen.Show();

        var scope = await _scopeLoader.Load(MenuScopeExtensions.LoadMenu);
        var loop = scope.Container.Container.Resolve<IMenuLoop>();
        var result = await loop.Process(scope.Lifetime);

        return result;
    }
}
```

**Responsibilities:**

- Loads Menu scope with UI services
- Manages UI state (loading screen, camera)
- Awaits player game selection
- Returns GameLoadData (GameMode + SessionData)

### GamePlayLoader

**File:** `Loaders/GamePlayLoader.cs`

Loads GamePlay scope and executes game:

```csharp
public class GamePlayLoader : IGamePlayLoader
{
    public async UniTask<IGameEndTransition> Load(GameLoadData gameLoadData)
    {
        await _loadingScreen.Show();
        _globalCamera.Enable();

        var scope = await _scopeLoader.Load(PvPScopeExtensions.LoadPvp);
        var loop = scope.Container.Container.Resolve<IPvPGameLoop>();
        var transitionData = await loop.Process(scope.Lifetime, gameLoadData.SessionData);

        return transitionData;
    }
}
```

**Responsibilities:**

- Loads PvP GamePlay scope with all game services
- Executes game via IPvPGameLoop
- Handles game result as IGameEndTransition
- Manages scope disposal on game end

---

## PvPGameLoop - Game State Machine

**File:** `../GamePlay/Loop/PvP/PvPGameLoop.cs`

Three-phase game state machine:

**Phase 1: WaitingForPlayers**

```csharp
_gameState.Set(GameStateType.WaitingFoPlayers);
await _session.Start(lifetime, sessionData.ServerUrl, sessionData.SessionId, _user.Id);
await UniTask.WaitUntil(() => _gameContext.All.Count == 2);
```

- Establish WebSocket connection to game server
- Wait for opponent connection
- Initialize both players in GameContext

**Phase 2: Active**

```csharp
_servicesInitializer.Init(lifetime);
_connection.OneWay(new MatchActionContexts.PlayerReady());
_gameState.Set(GameStateType.Active);

var gameResult = await _gameState.WaitCompletion(lifetime);
await _connection.ForceSendAll();
```

- Initialize gameplay services (board, cards, etc)
- Send PlayerReady command to server
- Execute game loop (input → server → sync)
- Wait for game completion

**Phase 3: Completed**

```csharp
_gameState.Set(GameStateType.Completed);
var transition = await _gameEnd.Process(lifetime, gameResult);
return transition;
```

- Show end-game screen
- Return IGameEndTransition (Exit or Rematch)
- Release resources via scope disposal

---

## MatchEventLoop - Event Dispatcher

**File:** `../GamePlay/Loop/Context/MatchEventLoop.cs`

Reactive state listener pattern:

```csharp
public void OnSetup(IReadOnlyLifetime lifetime)
{
    _state.Value.View(lifetime, (stateLifetime, state) =>
    {
        switch (state)
        {
            case GameStateType.WaitingForPlayers:
                OnWaitingForPlayers(stateLifetime);
                break;
            case GameStateType.Active:
                OnMatchStarted(stateLifetime, _context.Self);
                break;
            case GameStateType.Completed:
                OnMatchCompleted(stateLifetime, _state.CompletedData.Value);
                break;
        }
    });
}
```

**Features:**

- Invokes callbacks on state change
- Interfaces: IWaitingForPlayers, IMatchStarted, IMatchCompleted
- Subscribes multiple listeners via DI
- Creates new lifetime per state for cleanup

---

## Scope Hierarchy

```
InternalScope (foundation)
    ↓
GlobalScope (camera, UI, audio)
    ↓
MetaScope (user, backend)
    ↓
GameLoopScope (menu/game controller)
    │
    ├─ MenuScope (ephemeral, managed by GameLoopScopeLoader)
    │   ├─ Menu services
    │   ├─ Menu UI scenes
    │   └─ Social features
    │
    └─ GamePlayScope (ephemeral, managed by GameLoopScopeLoader)
        ├─ Board system
        ├─ Card system
        ├─ Player system
        ├─ Game network session
        └─ Game UI scenes
```

---

## Initialization Sequence

**From Startup:**

```
1. GameStartup.Setup()
2. Internal scope loaded
3. Global scope loaded
4. Meta scope loaded
5. GameLoop scope loaded
   ├── Services registered:
   │   ├── MenuLoader
   │   ├── GamePlayLoader
   │   └── GameLoopScopeLoader
   └── GameLoop.OnLoaded() starts
       └── Loop(lifetime) begins
           └── Menu().Forget() launches Menu
```

---

## Flow Transitions

### Menu → Game

```
MenuPlay.OnClicked()
  └─ Matchmaking.SearchGame()
      └─ Server finds opponent
      └─ SharedMatchmaking.GameResult projection update
      └─ MenuPlay.GameFound event emits SessionData
      └─ MenuLoop.Process() returns GameLoadData
      └─ GamePlayLoader.Load(GameLoadData) starts
      └─ Game phase begins
```

### Game → Menu (Exit)

```
Player clicks Exit
  └─ GameEnd.Process() executes
      └─ Returns GameEndTransition.Exit
      └─ GameLoop Game() catches Exit
      └─ Menu().Forget() launches Menu
      └─ Previous GamePlayScope disposed
      └─ New MenuScope created
```

### Game → Game (Rematch)

```
Player clicks Rematch
  └─ GameEnd.Process() executes
      └─ Matchmaking.CreateGame(opponent) sends request
      └─ Server creates new game
      └─ SharedMatchmaking.GameResult projection update
      └─ Returns GameEndTransition.Rematch with new SessionData
      └─ GameLoop Game() catches Rematch
      └─ Game(newSessionData).Forget() launches new game
      └─ Previous GamePlayScope disposed
      └─ New GamePlayScope created with new session
```

---

## Game State Lifecycle

```
WaitingForPlayers
  (Network connection, opponent search)
  │
  ├─ Both players connected
  │
  v
Active
  (Gameplay in progress)
  │
  ├─ Commands processed
  ├─ Board updates synced
  ├─ Cards drawn/used
  │
  ├─ Win condition met
  │
  v
Completed
  (Match finished)
  │
  ├─ End-game UI shown
  ├─ Winner determined
  ├─ Rematch or Exit decision
  │
  v
Loop continues (Menu or new Game)
```

---

## Key Interfaces

### Loaders
```csharp
IMenuLoader           // Menu scope loading
IGamePlayLoader       // GamePlay scope loading
IGameLoopScopeLoader  // Scope lifecycle management
```

### Game Flow
```csharp
IGameLoop             // Main orchestrator
IPvPGameLoop          // Game phase orchestrator
IGameEndTransition    // Exit or Rematch discriminated union
```

### State
```csharp
IGameState            // Match state machine
IGameRound            // Turn management
IGameContext          // Player and board hub
```

### Events
```csharp
IWaitingForPlayers    // Phase listener
IMatchStarted         // Phase listener
IMatchCompleted       // Phase listener
```

---

## Dependency Injection

**Service Registration (GameLoopScopeExtensions.cs):**

```csharp
builder.Register<GameLoop>()
    .As<IScopeLoaded>();  // Auto-invoked on initialization

builder.Register<GameLoopScopeLoader>()
    .WithParameter(parent)
    .As<IGameLoopScopeLoader>();

builder.Register<MenuLoader>()
    .As<IMenuLoader>();

builder.Register<GamePlayLoader>()
    .As<IGamePlayLoader>();
```

**Scope Initialization:**

```csharp
public static async UniTask<ILoadedScope> LoadGameLoop(
    this IServiceScopeLoader loader, ILoadedScope parent)
{
    var options = new ScopeLoadOptions(
        parent,
        loader.Assets.GetAsset<GameLoopServicesScene>(),
        Construct,
        false);

    var scope = await loader.Load(options);
    await scope.Initialize();  // Triggers GameLoop.OnLoaded()
    return scope;
}
```

---

## Fire-and-Forget Pattern

**Why `.Forget()` is used:**

```csharp
Menu().Forget();   // Explicitly non-awaited
Game(...).Forget(); // Shows intent clearly
```

**Rationale:**

- Signals intentional result discard
- Helps static analysis detect real unused tasks
- Prevents compiler warnings
- Makes code intent clear to future developers

---

## Critical Patterns

### Pattern 1: Nested Async Functions

```csharp
UniTask Loop()
{
    Menu().Forget();

    async UniTask Menu() { ... }
    async UniTask Game() { ... }
}
```

**Benefits:**
- State functions have direct access to outer scope
- Clean recursive flow without parameters
- Natural separation of state logic

### Pattern 2: Scope-Based Cleanup

```csharp
_currentScope?.Dispose().Forget();
_currentScope = newScope;
```

**Benefits:**
- Automatic resource cleanup
- No manual handler removal needed
- Prevents memory leaks

### Pattern 3: Reactive State Observation

```csharp
_state.Value.View(lifetime, (stateLifetime, state) =>
{
    // New lifetime per state
    // Auto-cleanup on state change
});
```

### Pattern 4: Discriminated Union for Results

```csharp
switch (transition)
{
    case GameEndTransition.Exit: /* ... */ break;
    case GameEndTransition.Rematch rematch: /* ... */ break;
}
```

---

## Assembly Dependencies

```
Loop.asmdef depends on:
  → Menu
  → GamePlay
  → Internal
  → Global
  → Meta
  → Common
  → Shared
  → VContainer
  → Cysharp.Threading.Tasks
```

---

## Key Files

| File | Purpose |
|------|---------|
| `GameLoop.cs` | Main state machine |
| `Loaders/GameLoopScopeLoader.cs` | Scope lifecycle |
| `Loaders/MenuLoader.cs` | Menu scope loading |
| `Loaders/GamePlayLoader.cs` | GamePlay scope loading |
| `Setup/GameLoopScopeExtensions.cs` | DI registration |

---

## See Also

- `OVERVIEW.md` - Application architecture
- `MENU.md` - Menu system
- `GAMEPLAY.md` - Game logic
- `COMMON_LIFETIMES.md` - Lifetime patterns
