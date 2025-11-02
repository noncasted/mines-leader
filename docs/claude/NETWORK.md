# Network System

Client-server communication bridge for entity synchronization, property updates, and event distribution.

## Overview

The Network System provides a complete framework for synchronizing game state between the Orleans backend and local Unity client. It handles WebSocket communication, command dispatching, property versioning, and automatic lifetime cleanup.

## Module Structure

```
Common/Network/
├── Common/
│   ├── Objects/         - INetworkObject and base abstractions
│   ├── Properties/      - INetworkProperty and synchronization
│   └── Events/          - INetworkEvents and event dispatch
├── Services/            - INetworkService and service management
├── Sessions/            - Session lifecycle and connections
├── Entities/            - INetworkEntity and entity management
├── Connections/         - WebSocket transport layer
├── Commands/            - Command definitions and handlers
├── Collections/         - Registry of active objects
└── Factories/           - Entity and service creation
```

## Architecture

### Data Flow

```
Network Data (MemoryPack serialized)
    ↓
NetworkConnection (WebSocket reader)
    ↓
CommandDispatcher (type routing)
    ↓
Command Handler (specific command type)
    ↓
Local State Update
```

### Key Components

| Component | Purpose |
|-----------|---------|
| `INetworkObject` | Base for all networked entities (properties, events, lifetime) |
| `INetworkProperty<T>` | Synced value with dirty tracking and versioning |
| `INetworkEvents` | Type-based event system for network objects |
| `INetworkConnection` | WebSocket transport layer |
| `INetworkService` | Long-lived server services (like Orleans grains) |
| `INetworkEntity` | Game entity with owner and lifetime |
| `NetworkCommandsDispatcher` | Routes network commands to handlers |
| `NetworkObjectsCollection` | Registry of active networked objects |

## Core Patterns

### Pattern 1: Property Synchronization

Properties are synchronized with versioning and dirty tracking for efficiency:

```csharp
// Backend sends update
await networkService.SetProperty(newValue);

// Client receives via NetworkConnection
NetworkProperty<T> receives update via MemoryPack

// Subscribers notified
IViewableProperty subscribers receive update via Advise(lifetime, handler)
```

**Key Features:**
- Dirty tracking (efficient updates)
- Versioning (prevent stale writes)
- MemoryPack serialization
- LifetimedValue subscription
- ViewableProperty for reactive clients

### Pattern 2: Entity Lifecycle

```
Create (NetworkEntityFactory)
  ↓
Register properties and events
  ↓
Network synchronization begins
  ↓
Subscribers listen via Advise(lifetime, handler)
  ↓
Destroy (EntityDestroyedCommand)
  ↓
Automatic cleanup via lifetime termination
```

### Pattern 3: Event Distribution

Type-based event system with automatic serialization:

```csharp
// Define event type
public class CardPlayedEvent
{
    public int CardId { get; set; }
    public Vector2Int Position { get; set; }
}

// Emit from backend
await networkService.EmitEvent(new CardPlayedEvent { ... });

// Subscribe on client
entity.Events.Advise<CardPlayedEvent>(
    lifetime,
    cardEvent => HandleCardPlayed(cardEvent)
);
```

**Features:**
- Serialized/deserialized via MemoryPack
- Inbound and outbound event support
- Automatic listener cleanup via lifetime

### Pattern 4: Command Processing

```
Network Data → Dispatcher → Type-specific handler → Local update
```

All network updates flow through the dispatcher which routes them to appropriate handlers based on command type.

### Pattern 5: Service Builder Pattern

Fluent configuration for network services:

```csharp
builder.RegisterNetworkService<MyService>(serviceId)
    .WithProperty<UserStateProperty>(propertyId)
    .WithProperty<ProfileDataProperty>(propertyId2)
    .Registration.As<IMyInterface>();
```

### Pattern 6: Entity Factory

Dynamic entity creation with automatic lifetime binding:

```csharp
var entity = await entityFactory.Create(entityId);
// Automatic lifetime binding
// Network synchronization begins
// Cleanup on lifetime termination
```

## Key Interfaces

### Foundation (35+ interfaces)

**Core Objects:**
```csharp
INetworkObject              // Base networked entity
INetworkProperty<T>         // Synced reactive property
INetworkEvents              // Event system
INetworkConnection          // WebSocket connection
```

**Services & Sessions:**
```csharp
INetworkService             // Long-lived network services
INetworkSession             // Session lifecycle
ISessionConnection          // Session-level connection
INetworkEntity              // Game entity
INetworkUser                // Connected player
```

**Collections:**
```csharp
INetworkObjectsCollection   // All active network objects
INetworkEntitiesCollection  // Specifically entities
INetworkUsersCollection     // Connected users
INetworkEntityIds           // Entity ID tracking
```

**Commands:**
```csharp
INetworkCommand             // Base command interface
EntityCreatedCommand        // Entity creation from server
EntityDestroyedCommand      // Entity deletion
EntityEventCommand          // Event triggering
EntityPropertyUpdateCommand // Property synchronization
```

## Dependency Injection

### Service Registration

```csharp
// Network services
builder.RegisterNetworkService<T>(serviceId)
    .WithProperty<StateT>(propertyId)
    .Registration.As<IMyInterface>();

// Network entity factory
builder.Register<INetworkEntityFactory>();

// Network connection
builder.Register<INetworkConnection>();

// Collections
builder.Register<INetworkObjectsCollection>();
```

### Lifetime Management

- All subscriptions use `Advise(lifetime, handler)`
- Automatic cleanup when lifetime terminates
- Reactive properties support hierarchical lifetimes
- ViewableProperty for client-side reactivity

## Logging Tags

```
[Network] [Connection]     - Network connection events (connect, disconnect)
[Network] [Sync]           - Property synchronization
[Network] [RPC]            - Remote procedure calls
[Network] [Message]        - Message reception and transmission
[Network] [Entity]         - Entity creation/destruction
[Network] [Event]          - Event distribution
```

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
- Common (Animation, Pooling)

## Key Files

| File | Purpose |
|------|---------|
| `Network/Common/Objects/INetworkObject.cs` | Base network abstraction |
| `Network/Common/Properties/INetworkProperty.cs` | Synced reactive property |
| `Network/Services/INetworkService.cs` | Long-lived services |
| `Network/Connections/NetworkConnection.cs` | WebSocket transport |
| `Network/Commands/NetworkCommandsDispatcher.cs` | Command routing |
| `Network/Entities/INetworkEntity.cs` | Game entity interface |
| `Network/Factories/NetworkEntityFactory.cs` | Entity creation |
| `Network/Collections/NetworkObjectsCollection.cs` | Object registry |

## Integration Points

- **Meta:** Backend projections use NetworkService pattern
- **Menu:** Network entities for social players
- **GamePlay:** Card and player network synchronization
- **Loop:** Session entity management
- **Common/Animations:** Network-driven sprite animations

## Critical Rules

1. **Always use lifetimes** - Subscribe with `Advise(lifetime, handler)`
2. **Versioning matters** - NetworkProperty prevents stale writes
3. **Dirty tracking** - Properties only update when changed
4. **Automatic cleanup** - No manual unsubscribe needed
5. **Type safety** - Use generics for command/event types
6. **Serialization** - All properties/events use MemoryPack

## See Also

- **docs/COMMON.md** - Animation and pooling systems
- **docs/COMMON_REACTIVE.md** - Reactive patterns
- **docs/COMMON_LIFETIMES.md** - Lifetime management
- **docs/META.md** - Backend projection system
- **docs/GAMEPLAY.md** - Entity synchronization in game
