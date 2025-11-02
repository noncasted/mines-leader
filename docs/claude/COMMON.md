# Client Common Module

Foundation layer providing animation system, object pooling utilities, and client-server network synchronization.

## Module Structure

```
Common/
├── Animations/        - Sprite animation framework
├── Network/          - Client-server communication and entity sync
├── Objects/          - Generic object pooling
├── Artwork/          - Art assets (non-code)
└── Assets/           - Shared resources
```

---

## Core Systems

### 1. Animation System (24 C# files)

**Purpose:** Sprite animation framework for dynamic game entities.

**Key Components:**

| Component | Purpose |
|-----------|---------|
| `IAnimation` | Play/PlayAsync/PlayLooped interface |
| `ISpriteAnimationRenderer` | Frame-to-sprite mapping |
| `IFrameProvider` | Frame selection logic (directional, rotatable) |
| `ILayerDefinition` | Animation layer organization |
| `SpriteAnimation` | Main animation controller |

**Directory Organization:**

- **Abstract/** - Core animation interface
- **Base/** - Base data structures and scriptable assets
- **Layers/** - Layer definitions and management
- **Sprites/** - Main sprite animation system
  - FrameProviders/ - Directional/rotational frame selection
  - Renderer/ - Rendering pipeline
  - Updatables/ - Update strategies (async, looped, void)

**Animation Modes:**

- Synchronous animation
- Async animation with UniTask support
- Looped animation with auto-restart
- Proper cleanup on lifetime termination

### 3. Object Pooling

**Purpose:** Generic template for creating and managing game objects.

**Components:**

- `ObjectFactory<T>` - Template for object creation and pooling
- Supports both instantiation and prefab-based creation
- Integrates with lifetime management for automatic cleanup

---

## Key Interfaces

### Animation Interfaces

```csharp
IAnimation                  // Animation controller
ISpriteAnimationRenderer    // Sprite rendering
IFrameProvider              // Frame selection
ILayerDefinition            // Animation layers
```

---

## Dependency Injection

**Service Registration:**

```csharp
// Animation
builder.Register<SpriteAnimation>();
builder.Register<SpriteAnimationRenderer>();

// Object pooling
builder.Register<ObjectFactory<T>>();

// For Network services, see docs/NETWORK.md
```

**Lifetime Management:**

- All subscriptions use `Advise(lifetime, handler)`
- Automatic cleanup when lifetime terminates
- Reactive properties support hierarchical lifetimes
- ViewableProperty for client-side reactivity

---

## Critical Patterns

### Pattern 1: Animation State Machine

```
Animation.Play() → Frame selection → Sprite update → Lifecycle cleanup
```

### Pattern 2: Async Animation Lifecycle

```csharp
var animation = container.Resolve<SpriteAnimation>();
await animation.PlayAsync(spriteFrames, duration, lifetime);
// Auto-cleanup when lifetime terminates
```

### Pattern 3: Layered Animation

```csharp
var renderer = GetComponent<ISpriteAnimationRenderer>();
renderer.SetLayer(0, sprite); // Base layer
renderer.SetLayer(1, effect); // Effect overlay
// Supports multiple simultaneous layers
```

### Pattern 4: Directional Frames

```csharp
// Frame provider selects correct sprite based on direction
var frameProvider = new DirectionalFrameProvider(spriteSets);
var sprite = frameProvider.GetFrame(direction);
```

---

## Logging Tags

```
[Animation] [Sprite]       - Sprite animation playback
[Animation] [Layer]        - Layer management
[Pooling] [Factory]        - Object factory operations
```

For network-related logging, see **docs/NETWORK.md**

---

## Assembly Dependencies

**Depends on:**
- Internal (Lifetime, DI, Reactive)
- Global (Updater, logging)
- Shared (Protocol definitions)
- VContainer (DI)
- UniTask (Async)
- MemoryPack (Serialization)

**Used by:**
- Meta (Backend projection)
- Menu (Network entities)
- GamePlay (Entity synchronization)
- Loop (Game sessions)

---

## Key Files

| File | Purpose |
|------|---------|
| `Animations/Abstract/IAnimation.cs` | Animation controller |
| `Animations/Sprites/SpriteAnimation.cs` | Main animation system |
| `Animations/Sprites/Renderer/ISpriteAnimationRenderer.cs` | Sprite rendering |
| `Animations/Sprites/FrameProviders/IFrameProvider.cs` | Frame selection logic |
| `Objects/ObjectFactory.cs` | Generic object pooling |

For network files, see **docs/NETWORK.md**

---

## Integration Points

- **Meta:** Backend projections use NetworkService pattern
- **Menu:** Network entities for social players
- **GamePlay:** Card and player network synchronization
- **Loop:** Session entity management

---

## See Also

- **docs/NETWORK.md** - Network synchronization and entity lifecycle
- **docs/COMMON_REACTIVE.md** - Reactive patterns
- **docs/COMMON_LIFETIMES.md** - Lifetime management
- **docs/GAMEPLAY.md** - Entity animation in game
- **docs/META.md** - Backend projection system
