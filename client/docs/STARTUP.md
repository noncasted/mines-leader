# Client Startup Module

Application bootstrap entry point orchestrating the initialization sequence for all systems.

## Module Structure

```
Startup/
├── GameStartup.cs       - Main entry point (MonoBehaviour)
├── Startup.asmdef       - Assembly definition
└── Configuration.cs     - Bootstrap configuration
```

---

## Bootstrap Sequence

### GameStartup.cs

Main entry point MonoBehaviour:

```csharp
public class GameStartup : MonoBehaviour
{
    private async void Awake()
    {
        await Setup();
    }

    private async UniTask Setup()
    {
        // 1. Create Internal foundation
        var internalScopeLoader = new InternalScopeLoader(_internal);
        var startScene = gameObject.scene;

        var internalScope = internalScopeLoader.Load();
        var scopeLoader = internalScope.Get<IServiceScopeLoader>();

        // 2. Load Global services
        var globalScope = await scopeLoader.LoadGlobal(internalScope);
        var globalCamera = globalScope.Get<IGlobalCamera>();
        var loadingScreen = globalScope.Get<ILoadingScreen>();
        globalCamera.Enable();
        loadingScreen.Show();

        // 3. Load Meta services (backend/user)
        var metaScope = await scopeLoader.LoadMeta(globalScope);

        // 4. Load Game Loop controller
        await scopeLoader.LoadGameLoop(metaScope);

        // 5. Unload startup scene
        await SceneManager.UnloadSceneAsync(startScene);
    }
}
```

**Key Points:**

- Runs in MonoBehaviour.Awake() before any game logic
- All phases are sequential async operations
- Startup scene unloaded after complete
- Services configured in strict order

---

## Scope Hierarchy

```
InternalScope (Foundation Layer)
    │
    ├─ IServiceScopeLoader
    ├─ ISceneLoader
    ├─ IEntityScopeLoader
    └─ Asset environment

    ↓ Parent reference

GlobalScope (Persistent Services Layer)
    │
    ├─ IGlobalCamera
    ├─ IGlobalControls
    ├─ IUpdater
    ├─ IAudioPlayer
    ├─ IBackendClient
    ├─ ISettings
    ├─ IUIStateMachine
    └─ ILoadingScreen

    ↓ Parent reference

MetaScope (Backend Integration Layer)
    │
    ├─ IUser
    ├─ IMetaBackend
    ├─ BackendProjectionHub
    ├─ Projections (reactive state sync)
    └─ IMatchmaking

    ↓ Parent reference

GameLoopScope (Game Flow Layer)
    │
    ├─ IMenuLoader
    ├─ IGamePlayLoader
    ├─ GameLoop (orchestrator)
    └─ GameLoopScopeLoader

        ├─ MenuScope (ephemeral)
        │   ├─ MenuLoop
        │   ├─ MenuNavigation
        │   ├─ MenuPlay
        │   └─ UI scenes
        │
        └─ GamePlayScope (ephemeral)
            ├─ PvPGameLoop
            ├─ Board system
            ├─ Card system
            ├─ Player system
            └─ Game UI scenes
```

---

## Initialization Phases

### Phase 1: Internal Scope (Synchronous)

**Duration:** ~50ms

```csharp
var internalScopeLoader = new InternalScopeLoader(_internal);
var internalScope = internalScopeLoader.Load();
```

**Initializes:**

1. VContainer container setup
2. Asset environment and caching
3. Platform options loading
4. Environment preprocessors execution
5. Scene and entity loaders registration

**Lifetime:** Application-wide

### Phase 2: Global Scope (Asynchronous)

**Duration:** ~200-400ms

```csharp
var globalScope = await scopeLoader.LoadGlobal(internalScope);
```

**Loading Sequence:**

```
1. ServiceScopeLoader.Load()
   ├─ Load GlobalServicesScene asset
   ├─ Create scope GameObject with LifetimeScope
   └─ Register all services

2. EventLoop construction
   ├─ OnBaseSetup() - all services synchronous setup
   └─ OnBaseSetupAsync() - async initialization
       ├─ Audio system setup
       ├─ Camera initialization
       ├─ Input system setup
       ├─ Updater initialization
       ├─ Backend client setup
       ├─ Settings loading from PlayerPrefs
       └─ Publisher (platform) initialization

3. UI initialization
   ├─ Global UI state machine created
   └─ Loading screen prefab instantiated

4. Camera and loading screen activation
```

**Services Registered:**

| Service | Lifetime | Purpose |
|---------|----------|---------|
| IAudioPlayer | Singleton | Music/SFX playback |
| IGlobalCamera | Singleton | World camera |
| IGlobalControls | Singleton | Input handling |
| IUpdater | Singleton | Frame update loop |
| IMessageBroker | Singleton | Event pub/sub |
| IBackendClient | Singleton | HTTP REST client |
| ISettings | Singleton | Player preferences |
| IUIStateMachine | Singleton | UI state transitions |
| ILoadingScreen | Singleton | Loading UI |

**Lifetime:** Application-wide (until exit)

### Phase 3: Meta Scope (Asynchronous)

**Duration:** ~500-2000ms (network dependent)

```csharp
var metaScope = await scopeLoader.LoadMeta(globalScope);
```

**Loading Sequence:**

```
1. ServiceScopeLoader.Load()
   ├─ Load MetaServicesScene asset
   ├─ Create scope GameObject
   └─ Register meta services

2. EventLoop construction
   └─ OnBaseSetupAsync()
       └─ MetaLoop.OnBaseSetupAsync()
           ├─ Authentication.Execute()
           │  ├─ Check PlayerPrefs for cached userId
           │  └─ SignUp or LogIn via HTTP
           │
           ├─ User.Init(userId)
           │  └─ Store user ID
           │
           ├─ MetaBackend.Connect()
           │  ├─ Create WebSocket connection
           │  ├─ Send SharedBackendSocketAuth with userId
           │  └─ Initialize NetworkConnection
           │
           └─ Wait for ConnectionCompletedCommand
              └─ Signals projections ready

3. Projection initialization
   ├─ BackendProjectionHub receives initial states
   ├─ SharedBackendUser.ProfileProjection
   ├─ SharedBackendUser.DeckProjection
   ├─ SharedMatchmaking.GameResult
   └─ SharedMatchmaking.LobbyResult

4. DeckService initialization
   └─ Listens to deck projection for updates
```

**Services Registered:**

| Service | Purpose |
|---------|---------|
| IUser | Current user holder |
| IMetaBackend | Backend facade |
| IMatchmaking | Game/lobby search |
| IDeckService | Deck state tracking |
| ICardsRegistry | Card definitions |
| BackendProjections | Reactive state sync |

**Lifetime:** Application-wide (parent of GameLoop)

### Phase 4: Game Loop Scope (Asynchronous)

**Duration:** ~100-200ms

```csharp
await scopeLoader.LoadGameLoop(metaScope);
```

**Loading Sequence:**

```
1. ServiceScopeLoader.Load()
   ├─ Load GameLoopServicesScene asset
   ├─ Create scope GameObject
   └─ Register loaders and orchestrator

2. EventLoop construction
   └─ OnLoaded()
       └─ GameLoop.OnLoaded()
           └─ Loop(lifetime).Forget()
               └─ Menu().Forget()  // Start with Menu
                   └─ MenuLoader.Load()
                       ├─ Enable camera
                       ├─ Show loading screen
                       ├─ Load MenuScope
                       └─ Start menu UI
```

**Services Registered:**

| Service | Purpose |
|---------|---------|
| GameLoop | Main orchestrator |
| IMenuLoader | Menu scope loading |
| IGamePlayLoader | GamePlay scope loading |
| IGameLoopScopeLoader | Scope lifecycle manager |

**Lifetime:** Application-wide (until exit)

### Phase 5: Startup Scene Unload

**Duration:** ~50-100ms

```csharp
await SceneManager.UnloadSceneAsync(startScene);
```

- Unloads the bootstrap scene
- Application ready for gameplay

---

## Configuration

### InternalScopeConfig

```csharp
public class InternalScopeConfig
{
    public GameObject InternalScopePrefab { get; set; }
    public PlatformType Platform { get; set; }
}
```

Defines:
- Internal scope prefab asset reference
- Target platform (Editor, WebGL, iOS, Android)

### Scene Requirements

**Startup Scene:**
- Single GameObject with GameStartup component
- Persists during bootstrap
- Unloaded after complete

**No other scenes required:**
- Services scenes are loaded programmatically
- Menu and GamePlay scenes loaded on demand

---

## Error Handling

**Startup Error Scenarios:**

1. **Authentication Failure**
   - SignUp fails → Exception bubbles to Awake()
   - Game cannot proceed without user ID
   - Application should retry or show error UI

2. **Backend Connection Failure**
   - WebSocket fails → MetaLoop.OnBaseSetupAsync() throws
   - Game cannot proceed without backend connection
   - ApplicationFlow should handle gracefully

3. **Asset Loading Failure**
   - Asset references null → NullReferenceException
   - Scene loading fails → SceneLoadingException
   - Invalid platform configuration → ConfigurationException

**Current Behavior:**
- Exceptions propagate without try-catch
- MonoBehaviour logs exception
- Application becomes unresponsive
- Future: Implement error screen and retry logic

---

## Logging

**No specific bootstrap logging tags.**

Components log via standard tags:
- `[Internal]` - Scope creation
- `[Backend]` - Backend connection
- `[Projection]` - Projection initialization
- `[UI]` - Loading screen

---

## Performance Metrics

**Expected Timings:**

| Phase | Typical | Range |
|-------|---------|-------|
| Internal (sync) | 50ms | 30-100ms |
| Global (async) | 250ms | 100-500ms |
| Meta (async, network) | 1000ms | 500-3000ms |
| GameLoop (async) | 150ms | 100-300ms |
| **Total** | **1450ms** | **800-3900ms** |

**Factors Affecting Duration:**

- Network latency (Meta phase)
- Asset loading performance (Global/Meta phases)
- Device CPU/memory (all phases)
- Platform (WebGL vs Native)

---

## Assembly Dependencies

```
Startup.asmdef depends on:
  → Internal (scope loading)
  → Global (services)
  → Loop (game orchestration)
  → Meta (backend)
  → Common (networking)
  → Shared (protocol)
  → VContainer (DI)
  → Cysharp.Threading.Tasks (async)
  → SceneManagement (scene loading)
```

---

## Key Files

| File | Purpose |
|------|---------|
| `GameStartup.cs` | Main entry point |
| `Configuration.cs` | Bootstrap configuration |

---

## Integration Points

**Depends on (called by):**
- Unity scene initialization
- MonoBehaviour lifecycle

**Called by:**
- Internal scope initialization
- Service scope loading
- Scene management

**Calls:**
- All bootstrap services
- Scene loading systems

---

## Critical Rules

1. **Strict Order** - Phases must execute sequentially (parent scope dependency)
2. **No Blocking UI** - All operations use async/UniTask
3. **Lifetime Management** - Each scope tracks cleanup via lifetime
4. **Error Propagation** - Exceptions should bubble to Awake()
5. **DontDestroyOnLoad** - Only InternalScope persists

---

## See Also

- `OVERVIEW.md` - Application architecture
- `INTERNAL.md` - Scope loading system
- `GLOBAL.md` - Global services
- `META.md` - Backend integration
- `LOOP.md` - Game flow orchestration
