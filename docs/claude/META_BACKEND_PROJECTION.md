# Meta Backend Projection System

This document describes the client-side Meta Backend Projection system, which provides reactive state synchronization between the backend Orleans actors and Unity client components.

## Overview

The Backend Projection system is a reactive data synchronization layer that:
- **Receives state updates** from backend Orleans actors via network messages
- **Manages projection lifecycles** with automatic memory cleanup
- **Provides reactive access** to backend state through the `IViewableProperty<T>` pattern
- **Type-safe routing** of projection updates to registered handlers

## Core Components

### `IBackendProjection<T>`
```csharp
public interface IBackendProjection<T> : IViewableProperty<T> where T : class, INetworkContext
```

The main interface for accessing projected backend state. Extends `IViewableProperty<T>` to provide:
- **`T Value`**: Current projection value (null if not received)
- **`IReadOnlyLifetime ValueLifetime`**: Lifetime scope of current value
- **`Advise(lifetime, handler)`**: Subscribe to value changes

### `BackendProjection<T>`
```csharp
public class BackendProjection<T> : IBackendProjection, IBackendProjection<T>
```

**[Meta] [Projection]** Core implementation that:
- **Stores current state** in a `LifetimedValue<T>` for automatic cleanup
- **Handles network updates** via `OnReceived(INetworkContext context)`
- **Manages subscriptions** through the reactive property pattern

**Key Features:**
- **Automatic lifecycle management**: Values are properly disposed when updated
- **Type safety**: Only accepts network contexts matching the generic type
- **Reactive notifications**: All subscribers are notified immediately on updates

### `BackendProjectionHub`
```csharp
public class BackendProjectionHub : OneWayCommand<SharedBackendProjection>
```

**[Meta] [Projection]** Central dispatcher that:
- **Routes incoming projections** to appropriate `IBackendProjection` instances
- **Maintains projection registry** by type for O(1) lookup
- **Logs projection activity** for debugging and monitoring

**Processing Flow:**
1. Receives `SharedBackendProjection` from network layer
2. Extracts `INetworkContext` and determines runtime type
3. Looks up registered projection handler by type
4. Calls `OnReceived()` on the appropriate projection instance

## Projection Types

The system currently supports these backend projections:

### User Projections
- **`SharedBackendUser.ProfileProjection`**: User profile data (ID, Name)
- **`SharedBackendUser.DeckProjection`**: User's deck configurations and selected deck

### Matchmaking Projections
- **`SharedMatchmaking.GameResult`**: Game session connection details
- **`SharedMatchmaking.LobbyResult`**: Lobby session connection details

## Usage Patterns

### Basic Subscription
```csharp
public class UserProfileComponent : ISceneService
{
    private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _userProfile;

    public void Setup(IReadOnlyLifetime lifetime)
    {
        // Listen for profile updates
        _userProfile.Listen(lifetime, OnProfileUpdated);
    }

    private void OnProfileUpdated(SharedBackendUser.ProfileProjection profile)
    {
        Debug.Log($"[User] Profile updated: {profile.Name}");
        // Update UI components
    }
}
```

### Async Waiting
```csharp
public async UniTask WaitForProfile(IReadOnlyLifetime lifetime)
{
    var profile = await _userProfile.WaitOnce(lifetime);
    Debug.Log($"[User] Received profile: {profile.Name}");
}
```

### Service Registration
```csharp
// In MetaServicesExtensions.cs
builder
    .RegisterBackendProjection<SharedBackendUser.ProfileProjection>()
    .RegisterBackendProjection<SharedBackendUser.DeckProjection>()
    .RegisterBackendProjection<SharedMatchmaking.GameResult>()
    .RegisterBackendProjection<SharedMatchmaking.LobbyResult>();
```

## Network Protocol

### Shared Types
**`SharedBackendProjection`**: Network wrapper containing any `INetworkContext` projection
```csharp
[MemoryPackable]
public partial class SharedBackendProjection : INetworkContext
{
    public INetworkContext Context { get; set; }
}
```

### Backend Registration
All projection types are registered in the shared union builder via `SharedBackendExtensions.AddSharedBackend()`, ensuring consistent serialization between client and server.

## Lifetime Management

**⚠️ IMPORTANT**: The projection system integrates with the DI lifetime management system:

- **Projections** are registered with automatic disposal
- **Current values** use `LifetimedValue<T>` for automatic cleanup when replaced
- **Subscriptions** are automatically cleaned up when the provided lifetime terminates
- **Child lifetimes** are created for async operations like `WaitOnce()`

## Best Practices

### DO
- **Use the `Listen()` extension** for immediate value + change notifications
- **Pass appropriate lifetimes** to ensure proper cleanup
- **Check for null values** before using projection data
- **Use `WaitOnce()` for one-time async operations**

### DON'T
- **Don't forget lifetime management** - always pass a valid `IReadOnlyLifetime`
- **Don't assume immediate availability** - projections may be null initially
- **Don't manually dispose** projections - let the DI system handle it
- **Don't create projections directly** - use the registration extensions

## Integration Points

### With Network Layer
- Automatically receives `SharedBackendProjection` messages from backend
- Integrates with the `OneWayCommand<T>` pattern for message handling

### With DI System
- Uses `IScopeBuilder.RegisterBackendProjection<T>()` for registration
- Automatically registered as both `IBackendProjection<T>` and `IBackendProjection`
- Participates in scope lifetime management

### With Backend Orleans
- Receives updates from Orleans grain state changes
- Maintains consistency with server-side state through reactive updates
- Supports real-time synchronization of user profiles, decks, and matchmaking results

## Debugging and Monitoring

The system includes comprehensive logging:
- **`[Projection]`** tag for all projection-related logs
- **Type information** logged for received projections
- **Missing handler warnings** when no projection is registered for a type

Example log output:
```
[Projection] Received projection of type: Shared.SharedBackendUser+ProfileProjection
[Projection] No projection for type: Shared.UnknownProjection
```