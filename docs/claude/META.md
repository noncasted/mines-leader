# Client Meta Module

Backend integration layer providing authentication, reactive state synchronization (projections), user management, and matchmaking coordination.

## Module Structure

```
Meta/
├── Auth/           - User authentication
├── Connection/     - Backend communication
├── Cards/          - Card definitions and registry
├── Decks/          - Player deck state
├── Loop/           - Initialization sequence
├── Matchmaking/    - Game search and lobby
├── Setup/          - Dependency injection
├── User/           - User identity
└── Root files      - Backend facade
```

---

## Core Systems

### Authentication

**File:** `Auth/Authentication.cs`

User authentication with caching:

```csharp
public class Authentication
{
    public async UniTask<Guid> Execute()
    {
        // 1. Check PlayerPrefs cache
        var cachedId = PlayerPrefs.GetString("UserId");
        if (cachedId != null)
        {
            await _backend.LogIn(cachedId);
            return cachedId;
        }

        // 2. Create new account
        var response = await _backend.SignUp("HUESOS");
        PlayerPrefs.SetString("UserId", response.UserId);
        return response.UserId;
    }
}
```

**Features:**

- Persistent user ID via PlayerPrefs
- Fallback account creation with username
- HTTP-based endpoint calls
- GUID-based user identification

### Backend Projection System

**File:** `Connection/BackendProjection.cs`

Type-safe reactive state wrapper:

```csharp
public class BackendProjection<T> : IBackendProjection<T>
{
    private readonly LifetimedValue<T> _lifetimedValue;

    public void OnReceived(INetworkContext context)
    {
        if (context is T typedContext)
            _lifetimedValue.Set(typedContext);
    }
}
```

**Usage Pattern:**

```csharp
public void OnSetup(IReadOnlyLifetime lifetime)
{
    _projection.Listen(lifetime, data =>
    {
        // Called immediately if value exists, then on each update
        UpdateUIFromData(data);
    });
}
```

### BackendProjectionHub

**File:** `Connection/BackendProjectionHub.cs`

Routes server updates to correct projection:

```csharp
public class BackendProjectionHub : INetworkCommand
{
    private Dictionary<Type, IBackendProjection> _projections = new();

    public void Execute(IReadOnlyLifetime lifetime, SharedBackendProjection context)
    {
        var type = context.Context.GetType();
        if (_projections.TryGetValue(type, out var projection))
            projection.OnReceived(context.Context);
    }
}
```

**Flow:**

```
Backend sends SharedBackendProjection
  └─ NetworkConnection deserializes
  └─ BackendProjectionHub.Execute()
      └─ Type dispatch: _projections[type]
      └─ BackendProjection<T>.OnReceived()
      └─ LifetimedValue<T>.Set()
      └─ All subscribers notified
```

### Registered Projections

**MetaServicesExtensions.cs lines 45-49:**

```csharp
.RegisterBackendProjection<SharedBackendUser.ProfileProjection>()
.RegisterBackendProjection<SharedBackendUser.DeckProjection>()
.RegisterBackendProjection<SharedMatchmaking.GameResult>()
.RegisterBackendProjection<SharedMatchmaking.LobbyResult>()
```

---

## User Management

**File:** `User/User.cs`

Current user holder:

```csharp
public class User
{
    public Guid Id { get; private set; }
    public CharacterType Character { get; } = CharacterType.BIBA;

    public void Init(Guid userId)
    {
        Id = userId;
    }
}
```

**Usage:**

```csharp
var currentUserId = _user.Id;  // Access throughout app
```

---

## MetaLoop - Initialization Orchestrator

**File:** `Loop/MetaLoop.cs`

Startup sequence:

```csharp
public class MetaLoop : IScopeSetup
{
    public async UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
    {
        // 1. Authentication
        var userId = await _authentication.Execute();
        _user.Init(userId);

        // 2. WebSocket connection
        await _backend.Connect(lifetime);
        // Sends SharedBackendSocketAuth.Request with userId

        // 3. Wait for projections ready
        await _connectionAwaiter.CompleteTask;
    }
}
```

**Phases:**

1. **Authentication** - Get/create user ID via HTTP
2. **Connection** - Establish WebSocket to Orleans backend
3. **Projection Sync** - Wait for all projections initialized

### ConnectionCompletedCommand

**File:** `Connection/ConnectionCompletedCommand.cs`

Signals initialization complete:

```csharp
public class ConnectionCompletedCommand : INetworkCommand
{
    public void Execute(IReadOnlyLifetime lifetime)
    {
        _connectionAwaiter.Complete();  // Signals MetaLoop wait
    }
}
```

---

## Backend Interface

**File:** `Connection/MetaBackend.cs`

Facade for backend communication:

```csharp
public interface IMetaBackend
{
    User User { get; }
    IBackendClient Client { get; }
    INetworkConnection Connection { get; }
    IReadOnlyLifetime Lifetime { get; }

    UniTask Connect(IReadOnlyLifetime lifetime);
}
```

**Provides:**

- Current user holder
- HTTP client for REST calls
- WebSocket connection manager
- Service lifecycle token

---

## Matchmaking System

### SearchGame

**File:** `Matchmaking/Matchmaking.cs`

Game creation and search:

```csharp
public class Matchmaking : IMatchmaking
{
    public async UniTask<SessionData> SearchGame(IReadOnlyLifetime lifetime)
    {
        // Wait for response projection
        var resultAwait = _gameResultProjection.WaitOnce(lifetime);

        // Send search command
        await _backend.Client.Post("/api/match/search", new SearchGameRequest());

        // Wait for server response via projection
        var result = await resultAwait;

        return new SessionData(result.ServerUrl, result.SessionId);
    }
}
```

**Pattern: Command-Response**

```
1. Set up wait for projection update (non-blocking)
2. Send command to backend
3. Await projection receives response
4. Extract data from response
```

### SearchLobby

**File:** `Matchmaking/Matchmaking.cs`

Social lobby search:

```csharp
public async UniTask<LobbyInfo> SearchLobby(IReadOnlyLifetime lifetime)
{
    var resultAwait = _lobbyResultProjection.WaitOnce(lifetime);
    await _backend.Client.Post("/api/match/search-lobby", ...);
    var result = await resultAwait;
    return new LobbyInfo(result.ServerUrl, result.LobbyId);
}
```

### CreateGame

**File:** `Matchmaking/Matchmaking.cs`

Game creation (from Menu after rematch):

```csharp
public async UniTask<SessionData> CreateGame(Guid opponentId)
{
    var resultAwait = _gameResultProjection.WaitOnce(lifetime);
    await _backend.Client.Post("/api/match/create", new CreateGameRequest { OpponentId = opponentId });
    var result = await resultAwait;
    return new SessionData(result.ServerUrl, result.SessionId);
}
```

---

## Deck Management

**File:** `Decks/DeckService.cs`

Player deck state tracking:

```csharp
public class DeckService : IScopeSetup
{
    private List<DeckConfiguration> _configurations = new();
    private IViewableProperty<int> _selectedIndex;

    public void OnSetup(IReadOnlyLifetime lifetime)
    {
        _projection.Listen(lifetime, data =>
        {
            foreach (var (index, entry) in data.Entries)
            {
                var configuration = GetOrCreateConfiguration(index);
                var cards = entry.Cards.Select(ct => _registry.Cards[ct]).ToList();
                configuration.Update(cards);
            }
            _selectedIndex.Set(data.SelectedIndex);
        });
    }
}
```

**Lifecycle:**

1. Listen to projection for deck updates
2. Update configurations from server data
3. Sync UI with latest state
4. Persist changes back to server

---

## Card System

**File:** `Cards/CardsRegistry.cs`

Centralized card definitions:

```csharp
public class CardsRegistry : ScriptableRegistry<CardDefinition>
{
    private Dictionary<CardType, ICardDefinition> _cards = new();

    protected override void OnInitialize()
    {
        foreach (var definition in Objects)
            _cards[definition.Type] = definition;
    }
}
```

**Shared Model:**

- `CardType` enum from Shared layer
- `CardDefinition` with UI metadata (sprites, descriptions)
- Backend holds actual card effects

---

## Network Protocol

### Endpoints

**Backend HTTP Endpoints:**

```csharp
// Authentication
SignUp(username) → UserId
LogIn(userId) → Success

// Matchmaking
SearchGame() → SessionData
CreateGame(opponentId) → SessionData
SearchLobby() → LobbyInfo
CancelSearch() → Success

// Update Operations
UpdateDeck(config) → Success
```

### Protocol Messages

**SharedBackendUser:**
- `ProfileProjection` - User profile (ID, name)
- `DeckProjection` - Player deck configuration
- `Match` - Match history entry

**SharedMatchmaking:**
- `Search` - Request to join queue
- `Create` - Create new game
- `CancelSearch` - Leave queue
- `GameResult` - Session details
- `LobbyResult` - Lobby details

**SharedConnectionCompleted:**
- Marker event for initialization completion

---

## Dependency Injection

**Service Registration (MetaServicesExtensions.cs):**

```csharp
builder
    .Register<MetaLoop>().As<IScopeSetup>()
    .Register<Authentication>()
    .Register<MetaBackend>().As<IMetaBackend>()
    .Register<User>().As<IUser>()
    .Register<DeckService>().As<IDeckService>()
    .Register<Matchmaking>().As<IMatchmaking>()
    .Register<CardsRegistry>().As<ICardsRegistry>()
    .Register<BackendProjectionHub>()
    .RegisterBackendProjection<SharedBackendUser.ProfileProjection>()
    .RegisterBackendProjection<SharedBackendUser.DeckProjection>()
    .RegisterBackendProjection<SharedMatchmaking.GameResult>()
    .RegisterBackendProjection<SharedMatchmaking.LobbyResult>()
    .Register<ConnectionCompletedCommand>();
```

---

## Scope Initialization

**File:** `Setup/MetaScopeExtensions.cs`

```csharp
public static async UniTask<ILoadedScope> LoadMeta(...)
{
    var scope = await loader.Load(options);
    await scope.Initialize();  // Triggers MetaLoop
    return scope;
}
```

**Initialization Phases:**

1. **OnBaseSetupAsync** - MetaLoop executes authentication and connection
2. **EventLoop construction** - All IScopeSetup handlers run
3. **Project initialization** - Projections receive initial values
4. **OnLoaded** - Scope ready for use

---

## Key Interfaces

### Authentication & Connection
```csharp
IAuthentication, IMetaBackend, IBackendClient
```

### Projections
```csharp
IBackendProjection<T>, IBackendProjectionHub
```

### User & Deck
```csharp
IUser, IDeckService, ICardsRegistry, ICardDefinition
```

### Matchmaking
```csharp
IMatchmaking, SessionData, GameLoadData
```

---

## Critical Patterns

### Pattern 1: Projection-Based Reactive Updates

```
Backend sends SharedBackendProjection
  └─ Hub receives and type-dispatches
  └─ BackendProjection<T>.OnReceived()
  └─ LifetimedValue.Set() with new value
  └─ All Advise subscribers notified
  └─ DeckService and UI components react
```

### Pattern 2: Command-Response Operations

```
Send command to backend
  └─ Set up wait for projection update
  └─ Backend processes
  └─ Sends SharedBackendProjection with result
  └─ Projection receives update
  └─ WaitOnce completes with result
```

### Pattern 3: Lifetime-Based Cleanup

```
Projection subscription
  └─ _projection.Advise(lifetime, handler)
  └─ Scope terminates
  └─ Lifetime terminates
  └─ Subscription removed
  └─ No dangling listeners
```

---

## Logging Tags

No specific tags defined. Uses component-specific tags:
- Backend operations: `[Backend]`
- Authentication: `[Auth]`
- Projections: `[Projection]`
- Matchmaking: `[Matchmaking]`

---

## Assembly Dependencies

```
Meta.asmdef depends on:
  → Common.Network (INetworkEntity, etc)
  → Global.Backend, Global.UI
  → Internal (Lifetime, DI, Reactive)
  → Shared (Protocol definitions)
  → VContainer, Cysharp.Threading.Tasks
  → MemoryPack
```

---

## Key Files

| File | Purpose |
|------|---------|
| `Loop/MetaLoop.cs` | Initialization orchestrator |
| `Auth/Authentication.cs` | User authentication |
| `Connection/MetaBackend.cs` | Backend facade |
| `Connection/BackendProjectionHub.cs` | Projection router |
| `Connection/BackendProjection.cs` | Reactive wrapper |
| `User/User.cs` | User holder |
| `Matchmaking/Matchmaking.cs` | Game/lobby search |
| `Decks/DeckService.cs` | Deck state management |
| `Cards/CardsRegistry.cs` | Card definitions |

---

## Integration Points

- **Startup** - Called after Global scope
- **Loop** - Provides data for MenuLoop and GameLoop
- **Menu** - User, matchmaking, deck service
- **GamePlay** - Card definitions, user info
- **Global** - Uses backend client and settings

---

## See Also

- `OVERVIEW.md` - Application architecture
- `LOOP.md` - Game flow
- `MENU.md` - Menu integration
- `GLOBAL.md` - Backend HTTP client
