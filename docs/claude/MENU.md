# Client Menu Module

User interface and social lobby system for matchmaking, deck management, and player interaction.

## Module Structure

```
Menu/
├── Common/          - Menu loop and initialization
├── Main/            - Primary menu screens
├── Screens/         - Secondary UI screens
├── Services/        - Shared UI services
├── Social/          - Social lobby system
├── Decks/           - Deck management UI
└── Artwork/         - Visual assets
```

---

## Core Components

### MenuLoop

**File:** `Common/Loop/MenuLoop.cs`

Main orchestrator for menu flow:

```csharp
public interface IMenuLoop
{
    UniTask<GameLoadData> Process(IReadOnlyLifetime lifetime);
}
```

**Responsibilities:**

1. Initialize social lobby connection
2. Display main menu with navigation
3. Handle game search via MenuPlay
4. Return GameLoadData when game found

### MenuSocialLoop

**File:** `Social/Loop/MenuSocialLoop.cs`

Initializes social lobby and player presence:

```csharp
public class MenuSocialLoop : IScopeSetup
{
    public async UniTask OnSetupAsync(IReadOnlyLifetime lifetime)
    {
        // 1. Search for/create social lobby
        var lobbyInfo = await _matchmaking.SearchLobby(lifetime);

        // 2. Connect to lobby
        await _session.Start(lifetime, url, lobbyId, userId);

        // 3. Spawn local player entity
        var localPlayer = await _playerFactory.Create(localUserId);

        // 4. Start listening for other players
        var collection = _playersCollection;
        collection.OnEntityCreated += player => SpawnVisual(player);
        collection.OnEntityDestroyed += player => DestroyVisual(player);
    }
}
```

**Features:**

- Establishes persistent social lobby connection
- Creates network entity for local player
- Synchronizes connected players
- Manages player lifetime and visibility

### MenuNavigation

**File:** `Main/Navigation/MenuNavigation.cs`

Main menu button navigation:

**Buttons:**

- Play (game search)
- Cards (card collection browser)
- Settings (player preferences)
- Shop (cosmetics/items)

**Implementation:**

```csharp
public class MenuNavigation : IScopeLoaded
{
    public void OnLoaded(IReadOnlyLifetime lifetime)
    {
        _playButton.OnClick += () => _uiStateMachine.ProcessChild(...);
        _cardsButton.OnClick += () => _uiStateMachine.ProcessChild(...);
        _settingsButton.OnClick += () => _uiStateMachine.ProcessChild(...);
        _shopButton.OnClick += () => _uiStateMachine.ProcessChild(...);
    }
}
```

### MenuPlay

**File:** `Main/Play/MenuPlay.cs`

Game search interface:

```csharp
public class MenuPlay : IScopeSetup
{
    public IViewableDelegate<SessionData> GameFound { get; }

    public async UniTask OnSetupAsync(IReadOnlyLifetime lifetime)
    {
        _button.OnClick += Search;
    }

    private async void Search()
    {
        _isInSearch.Set(true);
        var sessionData = await _matchmaking.SearchGame(_searchLifetime);
        GameFound.Invoke(sessionData);  // Emit to MenuLoop
    }
}
```

**Features:**

- Toggle search on/off
- Display search timer
- Emit SessionData when game found
- Cancel search on request

---

## Social System

### MenuPlayersCollection

**File:** `Social/Players/Collection/MenuPlayersCollection.cs`

Manages connected players in lobby:

```csharp
public class MenuPlayersCollection : INetworkEntitiesCollection
{
    private Dictionary<Guid, IMenuPlayer> _players = new();

    public IReadOnlyDictionary<Guid, IMenuPlayer> Players => _players;

    public void OnEntityCreated(INetworkEntity entity)
    {
        if (entity is IMenuPlayer player)
            _players[player.Id] = player;
    }

    public void OnEntityDestroyed(Guid entityId)
    {
        _players.Remove(entityId);
    }
}
```

**Lifetime Management:**

- Automatic cleanup on player disconnect
- Entity lifetimes govern visual persistence
- Collection updated via network events

### MenuPlayer

**File:** `Social/Players/Entity/MenuPlayer.cs`

Network entity for player presence:

```csharp
public interface IMenuPlayer : INetworkEntity
{
    Guid Id { get; }
    string Name { get; }
    IMenuPlayerChatView ChatView { get; }
    IMenuPlayerAnimator Animator { get; }
}
```

**Components:**

- `MenuPlayerView` - Visual representation (avatar, name)
- `MenuPlayerAnimator` - Animation state
- `MenuPlayerChatView` - Chat message display
- `MenuPlayerInput` - Movement input handling
- `MenuPlayerTransformState` - Network position/rotation

### MenuChat

**File:** `Social/Chat/MenuChat.cs`

Chat message distribution:

```csharp
public class MenuChat : INetworkService
{
    public UniTask OnNetworkEvent(MenuChatMessagePayload payload)
    {
        var player = _players.GetPlayer(payload.SenderId);
        player.ChatView.ShowMessage(payload.Message);
        return UniTask.CompletedTask;
    }
}
```

**Features:**

- NetworkService for message routing
- Type-safe message payload via MemoryPack
- Player-specific chat view display
- Auto-cleanup on disconnect

---

## Deck Management

### MenuDecks

**File:** `Decks/MenuDecks.cs`

Deck building UI and management:

```csharp
public class MenuDecks : IUIState, IUIStateAsyncEnterHandler
{
    private List<MenuDeckCard> _deckCards;
    private Dictionary<CardType, MenuDeckPoolSpot> _typeToPoolSpot;

    public async UniTask OnEnterAsync(IReadOnlyLifetime lifetime)
    {
        // Load deck configuration from service
        var config = await _deckService.LoadDeck();
        PopulateUI(config);
    }

    public void OnCardSelected(MenuDeckCard card)
    {
        // Update deck configuration
        _configuration.AddCard(card.Definition);
        _deckService.SaveDeck(_configuration);
        RecalculateMana();
    }
}
```

**Components:**

- `MenuDeckCard` - Visual slot for card in deck
- `MenuDeckPoolCard` - Available card preview
- `MenuDeckPoolSpot` - Card type grouping
- `MenuDeckIndexButton` - Deck selector
- `CardSelectionHighlight` - Selection indicator

**Features:**

- Drag-and-drop card selection
- Deck validation (card count, mana limits)
- Persistent storage via IDeckService
- Real-time mana calculation

---

## UI Screens

### MenuSettings

**File:** `Screens/Settings/MenuSettings.cs`

Player preferences interface:

```csharp
public interface IMenuSettings : IUIState
{
    // Displays:
    // - Master volume slider
    // - SFX/Music volume sliders
    // - Camera shake intensity slider
    // - VSync toggle
}
```

### MenuCards

**File:** `Screens/Cards/MenuCards.cs`

Card collection browser (stub):

```csharp
public interface IMenuCards : IUIState
{
    // Display available cards
    // Card filters and search
}
```

### MenuShop

**File:** `Screens/Shop/MenuShop.cs`

Shop/cosmetics interface (stub):

```csharp
public interface IMenuShop : IUIState
{
    // Shop items and cosmetics
}
```

---

## UI State Machine Integration

**Hierarchy:**

```
MenuMain (root state)
├── MenuNavigation (navigation buttons)
├── MenuPlay (game search screen)
├── MenuDecks (deck building)
├── MenuSettings (preferences)
├── MenuCards (collection browser)
└── MenuShop (cosmetics shop)
```

**State Transitions:**

```csharp
_uiStateMachine.ProcessChild(
    lifetime,
    menuDecksState,
    onComplete: () => ReturnToMain()
);
```

**Input Constraints:**

When UI is open:
- Game input disabled
- Menu input enabled
- Camera controls disabled

---

## Service Integration

### Matchmaking Integration

```
MenuPlay
  ├─ _matchmaking.SearchGame(lifetime)
  │  └─ Server finds opponent
  │  └─ Returns SessionData
  └─ GameFound event emits to MenuLoop
```

### User Services Integration

```
MenuSocialLoop
  ├─ _user.Id (current player)
  ├─ _session (network connection)
  └─ _playerFactory (entity creation)
```

### Deck Service Integration

```
MenuDecks
  ├─ _deckService.LoadDeck()
  ├─ _deckService.SaveDeck(config)
  └─ _cardsRegistry (card definitions)
```

### Settings Integration

```
MenuSettings
  ├─ _settings.MasterVolume
  ├─ _settings.SoundsVolume
  ├─ _settings.MusicVolume
  ├─ _settings.ShakeIntensity
  └─ _settings.VSync
```

---

## Dependency Injection

**Service Registration (MenuScopeExtensions.cs):**

```csharp
builder
    .AddMenuLoop()           // IMenuLoop
    .AddSessionServices()    // INetworkSession, matchmaking
    .AddMenuSocial()         // Social features
    .AddMenuUI()             // UI components
    .FindOrLoadSceneWithServices<MenuUIScene>();
```

**Key Services:**

```csharp
builder.Register<MenuLoop>().As<IMenuLoop>();
builder.Register<MenuSocialLoop>().As<IScopeSetup>();
builder.Register<MenuNavigation>().As<IScopeLoaded>();
builder.Register<MenuPlay>().As<IScopeSetup>();
builder.Register<MenuPlayersCollection>().As<INetworkEntitiesCollection>();
builder.Register<MenuChat>().As<INetworkService>();
builder.Register<MenuPlayerFactory>().As<IMenuPlayerFactory>();
```

---

## Lifetime Management

**Scope Hierarchy:**

```
Menu Scope (parent)
├── MenuLoop lifetime
├── MenuSocialLoop lifetime
├── MenuNavigation lifetime
├── Player entity lifetimes (per player)
├── UI state lifetimes (per screen)
└── Auto-cleanup on scope disposal
```

**Entity Lifetimes:**

```
Player Network Entity Created
  └─ MenuPlayer lifetime begins
  ├─ MenuPlayerView spawned
  ├─ MenuPlayerAnimator initialized
  └─ On disconnect: lifetime terminated
      └─ Auto-cleanup of view and animator
```

---

## Key Interfaces

### Menu Loop
```csharp
IMenuLoop          // Main orchestrator
IMenuSocialLoop    // Social lobby initialization
```

### Social
```csharp
IMenuPlayer        // Player network entity
IMenuPlayersCollection  // Player roster
IMenuChat          // Chat service
IMenuPlayerFactory // Player entity creation
```

### UI
```csharp
IMenuNavigation    // Button navigation
IMenuPlay          // Game search
IMenuDecks         // Deck management
IMenuSettings      // Player settings
IMenuCards         // Card browser
IMenuShop          // Shop
```

### Integration
```csharp
IMatchmaking       // Game/lobby search
IUser              // Current player
INetworkSession    // Network connection
IDeckService       // Deck persistence
ISettings          // Player preferences
IUIStateMachine    // UI state transitions
```

---

## Data Structures

### GameLoadData

Passed from Menu to GamePlay:

```csharp
public class GameLoadData
{
    public GameMode GameMode { get; set; }
    public SessionData SessionData { get; set; }
}
```

### SessionData

Network connection info:

```csharp
public class SessionData
{
    public string ServerUrl { get; set; }
    public Guid SessionId { get; set; }
}
```

### MenuChatMessagePayload

Chat message format:

```csharp
public class MenuChatMessagePayload
{
    public Guid SenderId { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
```

---

## Critical Patterns

### Pattern 1: Network Entity Management

```
MenuPlayerFactory.Create(userId)
  ├─ Create INetworkEntity via factory
  ├─ Spawn visual prefab
  ├─ Register in MenuPlayersCollection
  └─ On disconnect: lifetime terminates
      └─ Collection removes player
      └─ Visuals destroyed
```

### Pattern 2: UI State Transitions

```
MenuNavigation button click
  └─ UIStateMachine.ProcessChild()
      ├─ Create state lifetime
      ├─ Enter state
      ├─ Await completion
      ├─ Exit state
      └─ Terminate lifetime
```

### Pattern 3: Chat Display

```
NetworkEvent (MenuChatMessagePayload)
  └─ MenuChat handler
      └─ Get player from collection
      └─ Show message in chat view
      └─ Auto-hide after delay
```

---

## Assembly Dependencies

```
Menu.asmdef depends on:
  → Common.Network
  → Global.UI, Global.Inputs, Global.Settings, Global.Cameras
  → Meta (User, Matchmaking, CardDefinition)
  → Internal (DI, Lifetime, Reactive)
  → Shared (SessionData, GameMode)
  → VContainer, Cysharp.Threading.Tasks
```

---

## Key Files

| File | Purpose |
|------|---------|
| `Common/Loop/MenuLoop.cs` | Main orchestrator |
| `Social/Loop/MenuSocialLoop.cs` | Social lobby |
| `Main/Navigation/MenuNavigation.cs` | Button navigation |
| `Main/Play/MenuPlay.cs` | Game search |
| `Social/Players/MenuPlayersCollection.cs` | Player roster |
| `Social/Chat/MenuChat.cs` | Chat service |
| `Decks/MenuDecks.cs` | Deck management |

---

## See Also

- `LOOP.md` - Game flow orchestration
- `GAMEPLAY.md` - Game logic
- `META.md` - Backend integration
- `GLOBAL.md` - UI framework
