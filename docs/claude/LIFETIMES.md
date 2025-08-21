# LIFETIMES.md

## Overview

The Lifetime system is a core resource management pattern in this Unity project that provides automatic cleanup and memory management through hierarchical lifetime scopes. It prevents memory leaks, handles async operation cancellation, and manages event subscriptions automatically.

## Core Components

### Interface Hierarchy
- **`IReadOnlyLifetime`** - Base interface for lifetime observation
  - Provides `CancellationToken` for async operations
  - Allows listening for termination events
  - Read-only access to lifetime state

- **`ILifetime`** - Extends `IReadOnlyLifetime` with mutation capabilities
  - Adds `Terminate()` method for explicit termination
  - Used for controlling lifetime scope lifecycle

### Key Classes

**`Lifetime`** (`Assets/Internal/Common/Reactive/Lifetimes/Lifetime.cs`)
- Main implementation with parent-child relationships
- Manages termination listeners and cancellation tokens
- Supports hierarchical termination (children terminate when parent terminates)

**`TerminatedLifetime`** - Singleton pattern for already-terminated lifetimes
- Always returns `IsTerminated = true`
- No-op implementations for all methods
- Used as optimization for dead objects

**`GameObjectLifetime`** - Unity MonoBehaviour integration
- Automatically terminates when GameObject is disabled/destroyed
- Provides `GetValidLifetime()` for safe lifetime access

## Best Practices

### ✅ DO's

• **Always pass lifetimes to async operations:**
  ```csharp
  await animation.PlayAsync(lifetime);
  ```

• **Use lifetime for event subscription cleanup:**
  ```csharp
  inputAction.performed += OnAction;
  lifetime.Listen(() => inputAction.performed -= OnAction);
  ```

• **Create child lifetimes for scoped operations:**
  ```csharp
  var childLifetime = parentLifetime.Child();
  ```

• **Check termination before long operations:**
  ```csharp
  if (lifetime.IsTerminated) return;
  ```

• **Use GameObject extensions for Unity components:**
  ```csharp
  var lifetime = this.GetObjectLifetime(); // Automatic cleanup
  ```

• **Intersect lifetimes for complex dependencies:**
  ```csharp
  var combinedLifetime = lifetimeA.Intersect(lifetimeB);
  ```

• **Use CancellationToken for UniTask operations:**
  ```csharp
  await UniTask.Delay(1000, cancellationToken: lifetime.Token);
  ```

### ❌ DON'Ts

• **Don't forget to pass lifetimes to long-running operations**
  ```csharp
  // BAD: No lifetime passed
  animation.Play();
  
  // GOOD: Lifetime ensures cleanup
  animation.Play(lifetime);
  ```

• **Don't manually unsubscribe when using lifetimes**
  ```csharp
  // BAD: Manual unsubscribe
  event.AddListener(handler);
  // ... later manually remove
  
  // GOOD: Automatic cleanup
  event.Listen(lifetime, handler);
  ```

• **Don't create lifetimes without parents for temporary operations**
  ```csharp
  // BAD: Orphaned lifetime
  var lifetime = new Lifetime();
  
  // GOOD: Child of existing scope
  var lifetime = parentLifetime.Child();
  ```

• **Don't ignore IsTerminated checks in hot paths**
  ```csharp
  // BAD: Continuing after termination
  while (true) { DoWork(); }
  
  // GOOD: Respect termination
  while (!lifetime.IsTerminated) { DoWork(); }
  ```

## Usage Patterns

### Animation System Integration
```csharp
// All animations accept lifetime for automatic cancellation
animation.Play(lifetime, startTime: 0.5f);
await animation.PlayAsync(lifetime);
animation.PlayLooped(lifetime); // Stops when lifetime terminates
```

### Input System Integration  
```csharp
// Convert InputActions to reactive properties
var moveProperty = moveAction.ToProperty<Vector2>(lifetime);
var jumpFlag = jumpAction.ToFlag(lifetime);
```

### GameObject Integration
```csharp
// Button click handling with automatic cleanup
button.ListenClick(() => OnButtonClicked());

// Manual lifetime retrieval
var objectLifetime = this.GetObjectLifetime();
```

### Event Source Patterns
```csharp
// LifetimedValue - value changes create new lifetimes
var value = new LifetimedValue<int>(42);
value.Advise(lifetime, (valueLifetime, newValue) => {
    // valueLifetime terminates when value changes again
});
```

### Service Scope Integration
```csharp
// Service loading with lifetime management
await scopeLoader.LoadGlobal(parentLifetime);
// All services in scope terminate when parent terminates
```

## Advanced Patterns

### Lifetime Intersection
```csharp
// Terminates when EITHER lifetime terminates
var intersection = userLifetime.Intersect(sessionLifetime);
```

### Async Wait Patterns
```csharp
// Wait for event with lifetime cancellation
await lifetime.WaitInvoke(
    callback => eventSource.Subscribe(callback),
    callback => eventSource.Unsubscribe(callback)
);
```

### Hierarchical Termination
```csharp
// Parent-child relationship ensures proper cleanup order
var gameLifetime = new Lifetime();
var levelLifetime = gameLifetime.Child(); // Terminates with game
var playerLifetime = levelLifetime.Child(); // Terminates with level
```

## Memory Management

• Lifetimes automatically handle event unsubscription
• CancellationTokens prevent async operations from continuing after termination  
• Parent-child relationships ensure cleanup happens in correct order
• GameObject integration ties lifetime to Unity's lifecycle
• TerminatedLifetime prevents unnecessary allocations for dead objects

## Integration Points

- **Animation System**: All play methods accept `IReadOnlyLifetime`
- **Input System**: Extensions convert InputActions to reactive properties  
- **UI System**: Button clicks and event handling
- **Service Scopes**: Hierarchical service lifetime management
- **Async Operations**: UniTask cancellation via CancellationToken
- **Event Systems**: Reactive programming with automatic cleanup