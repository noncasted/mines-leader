# Client Global Module

Application-wide foundation providing camera, input, audio, UI, settings, and backend communication infrastructure.

## Module Structure

```
Global/
├── Setup/          - Scope initialization
├── Backend/        - HTTP/WebSocket client
├── Systems/        - Core update loops and events
├── Audio/          - Music and sound management
├── Cameras/        - Camera control
├── Inputs/         - Input handling system
├── UI/             - Global UI framework
├── Settings/       - Player configuration
├── Publisher/      - Platform integration
└── Constants/      - Game-wide constants
```

---

## Core Systems

### 1. Backend Communication (7 C# files)

**Purpose:** HTTP REST and WebSocket client for backend services.

**Components:**

```csharp
IBackendClient          // Main facade
├── IBackendGet        // HTTP GET with deserialization
├── IBackendPost       // HTTP POST with serialization
└── IBackendMedia      // Audio/Image downloads

BackendOptions         // Configuration asset (URLs)
```

**Features:**

- Environment configuration (Local/Production)
- JSON serialization via Unity's standard method
- Error handling with exception throwing
- Custom request headers support
- Lifetime-aware cancellation (respects IReadOnlyLifetime)
- MemoryPack binary serialization support

**Usage:**

```csharp
// HTTP GET with deserialization
var response = await backendClient.Get<UserData>("/api/user");

// HTTP POST with body
await backendClient.Post("/api/action", new { data = value });

// Media download
var audio = await backendClient.GetAudio("url", AudioType.OGG);
var texture = await backendClient.GetImage("url");
```

### 2. Update System (3 C# files)

**Purpose:** Frame-based update dispatcher with lifetime management and time scaling.

**Components:**

```csharp
IUpdater                    // Main update service
├── IPreUpdatable          // Updates before normal
├── IUpdatable             // Standard frame updates
├── IGizmosUpdatable       // Physics debug rendering
├── IFixedUpdatable        // Physics updates
├── IPreFixedUpdatable     // Physics pre-updates
└── IPostFixedUpdatable    // Physics post-updates

GlobalFrameUpdater         // Actual implementation
```

**Features:**

- Frame-based update system with delta time
- Time scaling support (Pause/Continue)
- Automatic cleanup when lifetime terminates
- Removal of terminated targets
- Integration with UniTask for async operations

### 3. Message Broker (2 C# files)

**Purpose:** Type-safe in-process pub/sub system for event distribution.

**Components:**

```csharp
IMessageBroker             // Pub/sub facade
Msg                        // Static global access point
```

**Usage:**

```csharp
// Publish event
Msg.Publish<GameStartedEvent>(new() { GameMode = PvP });

// Subscribe
Msg.Listen<GameStartedEvent>(lifetime, evt =>
{
    // Handle event
});
```

**Features:**

- Generic type-based routing
- Automatic listener cleanup on lifetime termination
- Fire-and-forget event dispatch
- Multiple subscribers per type

### 4. Audio System (4 C# files)

**Purpose:** Music and sound effects playback with volume management.

**Components:**

```csharp
IAudioPlayer               // Sound/music playback
IAudioListener             // Scene listener setup
IAudioVolume               // Volume control

AudioPlayer                // Actual implementation
AudioListener              // AudioListener component setup
```

**Features:**

- Master volume control
- Per-line volume (Music, SFX)
- Mute/Unmute support
- Looped music playback
- One-shot sound effects
- Persistent volume settings (PlayerPrefs)
- Save/restore functionality

### 5. Camera System (3 C# files)

**Purpose:** Global camera management and active camera tracking.

**Components:**

```csharp
IGlobalCamera              // World camera management
ICurrentCamera             // Active camera tracker
ICameraUtils              // Camera utility functions
```

**Features:**

- Global camera setup and positioning
- Enable/Disable lifecycle
- Active camera property tracking
- Camera utility functions for gameplay

### 6. Input System (3 C# files)

**Purpose:** Unified input handling with UI constraint support.

**Components:**

```csharp
IGlobalControls           // InputSystem wrapper
IInputConstraintsStorage  // UI input blocking

GlobalControls            // Control wrapper
InputConstraints          // Block/unblock input types
```

**Features:**

- Unity InputSystem integration
- Enable/Disable lifecycle
- UI input blocking when menus open
- Ref-counted constraint system
- Extension methods for reactive binding
- InputAction to IViewableProperty<T> conversion

### 7. UI Framework (3 C# files)

**Purpose:** Hierarchical UI state machine and loading screens.

**Components:**

```csharp
IUIStateMachine            // State transitions
ILoadingScreen             // Global loading screen
```

**Features:**

- Hierarchical state machine (parent/child states)
- Modal and stacked state support
- Input constraint integration
- Async state transitions
- Global loading screen management
- Design system (buttons, colors, scales, text effects)

### 8. Settings System (2 C# files)

**Purpose:** Player configuration persistence and runtime access.

**Components:**

```csharp
ISettings                  // Settings access
```

**Properties (all IViewableProperty<T>):**

```csharp
IViewableProperty<float> MasterVolume    // Master volume (0-1)
IViewableProperty<float> SoundsVolume    // SFX volume
IViewableProperty<float> MusicVolume     // Music volume
IViewableProperty<float> ShakeIntensity  // Camera shake intensity
IViewableProperty<bool> VSync            // Vertical sync
```

**Lifecycle:**

1. **OnSetupAsync** - Load settings from ISaves
2. **Apply defaults** - If not previously changed
3. **Push to properties** - Update reactive values
4. **Open()** - Show settings UI with real-time changes
5. **Persist** - Save to ISaves on apply

### 9. Platform Integration (Publisher)

**Purpose:** Platform-specific functionality (Itch.io, web, mobile).

**Components:**

```csharp
ISaves                      // Save/load functionality
ISystemLanguageProvider     // System language detection
IJsErrorCallback            // JS error handling

PlatformOptions             // Platform type selection
```

**Supported Platforms:**

- Website (web build)
- ItchIO (itch.io integration)
- iOS (Apple)
- Android (Google)

---

## Scope Initialization

**File:** `GlobalScopeExtensions.cs`

**Registration Order:**

```csharp
builder
    .AddAudio()              // Music/SFX system
    .AddCamera()             // Camera setup
    .AddInput()              // Input handling
    .AddSystemUtils()        // Updater, delays
    .AddBackend()            // Backend client
    .AddSettings()           // Player settings
    .AddPublisher()          // Platform integration
    .AddUI();                // UI framework
```

**Lifetime:** Application-wide persistence

---

## Configuration

### Backend Options

```csharp
BackendOptions (EnvAsset)
├── Local Mode
│   ├── WebSocket: ws://localhost:XXXX
│   └── HTTP: http://localhost:XXXX
└── Production Mode
    ├── WebSocket: wss://api.production.com
    └── HTTP: https://api.production.com
```

### Audio Options

```csharp
GlobalAudioOptions
├── AudioPlayer prefab reference
└── AudioListener prefab reference
```

### Camera Options

```csharp
GlobalCameraOptions
└── Camera prefab at (-0, 0, -10)
```

### UI Options

```csharp
GlobalUIOptions
└── Loading screen prefab
```

### Settings Defaults

```csharp
SettingsOptions
├── Default volume levels
├── Default shake intensity
├── Default VSync setting
└── Applied if not previously changed
```

---

## Key Interfaces

### Backend
```csharp
IBackendClient, IBackendGet, IBackendPost, IBackendMedia, BackendOptions
```

### Systems
```csharp
IUpdater, IMessageBroker, IDelayRunner, IApplicationFlow
```

### Audio
```csharp
IAudioPlayer, IAudioListener, IAudioVolume
```

### Camera
```csharp
IGlobalCamera, ICurrentCamera, ICameraUtils
```

### Input
```csharp
IGlobalControls, IInputConstraintsStorage, InputConstraints
```

### UI
```csharp
IUIStateMachine, ILoadingScreen, IUIConstraints, IDesignButton
```

### Settings
```csharp
ISettings, SettingsSave
```

### Platform
```csharp
ISaves, ISystemLanguageProvider, IJsErrorCallback, PlatformOptions
```

---

## Dependency Injection

**Service Registration (GlobalScopeExtensions.cs):**

```csharp
// Audio
builder.Register<AudioPlayer>().As<IAudioPlayer>();
builder.Register<AudioListener>().As<IAudioListener>();
builder.RegisterComponent<AudioVolume>().As<IAudioVolume>();

// Camera
builder.Register<GlobalCamera>().As<IGlobalCamera>();
builder.Register<CurrentCamera>().As<ICurrentCamera>();

// Input
builder.Register<GlobalControls>().As<IGlobalControls>();
builder.Register<InputConstraintsStorage>().As<IInputConstraintsStorage>();

// Systems
builder.Register<GlobalFrameUpdater>().As<IUpdater>();
builder.Register<MessageBroker>().As<IMessageBroker>();

// Backend
builder.Register<BackendClient>().As<IBackendClient>();

// Settings
builder.Register<Settings>().As<ISettings>();

// UI
builder.Register<UIStateMachine>().As<IUIStateMachine>();
builder.RegisterComponent<GlobalLoadingScreen>().As<ILoadingScreen>();
```

---

## Startup Integration

**Called from:** `Startup/GameStartup.cs`

**Sequence:**

```
1. Internal scope created (foundation)
2. Global scope loaded via ServiceScopeLoader
   ├── GlobalServicesScene asset loaded
   ├── All services registered
   ├── EventLoop.OnSetup and OnSetupAsync executed
   ├── Camera enabled
   └── Loading screen shown
3. Meta scope loaded (user/backend)
4. GameLoop scope loaded (menu/game)
```

---

## Logging

No specific logging tags for Global module. Logs use component-specific tags:
- Backend uses `[Backend]` tag
- UI uses `[UI]` tag
- Audio uses `[Audio]` tag
- Settings uses `[Settings]` tag

---

## Assembly Dependencies

**Depends on:**
- Internal (Lifetime, DI, Reactive, Scenes)
- Shared (Domain models)
- VContainer (DI)
- UniTask (Async)
- TextMeshPro (UI)
- Unity core (AudioClip, InputSystem, etc.)

**Used by:**
- Meta (Backend client)
- Menu (UI system)
- GamePlay (Camera, Input)
- Loop (Loading screen, camera control)
- All modules (Updater, Input, UI)

---

## Key Files

| File | Purpose |
|------|---------|
| `Setup/GlobalScopeExtensions.cs` | Service registration |
| `Backend/BackendClient.cs` | HTTP/WebSocket client |
| `Systems/Updaters/GlobalFrameUpdater.cs` | Frame update loop |
| `Systems/MessageBrokers/MessageBroker.cs` | Event pub/sub |
| `Audio/AudioPlayer.cs` | Audio controller |
| `Cameras/GlobalCamera.cs` | Camera management |
| `Inputs/GlobalControls.cs` | Input wrapper |
| `Settings/Settings.cs` | Settings service |

---

## See Also

- `OVERVIEW.md` - Application architecture
- `COMMON.md` - Network layer
- `META.md` - Backend integration
- `COMMON_LIFETIMES.md` - Lifetime management
