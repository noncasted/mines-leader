# Common: Lifetimes

Lightweight lifetime management system for resource cleanup and subscription scoping.

## Core Concept

```
Parent Lifetime -----> Child Lifetime -----> Child's Child
       |                     |                    |
   Terminate()          Terminate()           Terminate()
       |                     |                    |
       V                     V                    V
   All children         All children         (leaf node)
   terminated           terminated
```

**Key Principle:** When parent terminates -> all children terminate automatically.

## Interfaces

### IReadOnlyLifetime (read-only access)

```csharp
public interface IReadOnlyLifetime {
    CancellationToken Token { get; }    // For async operations
    bool IsTerminated { get; }          // Check state

    void Listen(Action callback);       // Subscribe to termination
    void RemoveListener(Action callback);
}
```

### ILifetime (full control)

```csharp
public interface ILifetime : IReadOnlyLifetime {
    void Terminate();  // Manually end lifetime
}
```

## API

### Creating Lifetimes

```csharp
// Standalone lifetime
var lifetime = new Lifetime();

// Child lifetime (terminates when parent terminates)
var child = parentLifetime.Child();

// From CancellationToken
var lifetime = cancellationToken.ToLifetime();
```

### Hierarchical Termination

```csharp
var parent = new Lifetime();
var child = parent.Child();
var grandchild = child.Child();

parent.Terminate();
// Result: parent, child, grandchild - all terminated
```

### Intersection (first-wins termination)

```csharp
// Terminates when EITHER lifetime terminates
var intersection = lifetimeA.Intersect(lifetimeB);
```

### Listening to Termination

```csharp
lifetime.Listen(() => {
    // Cleanup code here
    // Called once when lifetime terminates
});

// If lifetime already terminated - callback fires immediately
```

### CancellationToken Integration

```csharp
// Use with async operations
await Task.Delay(1000, lifetime.Token);

// Check cancellation
while (lifetime.IsTerminated == false) {
    // Work loop
}
```

## Special Implementation

### TerminatedLifetime

Pre-terminated lifetime. All operations are no-ops.

```csharp
var terminated = new TerminatedLifetime();
terminated.IsTerminated;  // always true
terminated.Listen(cb);    // callback NOT fired (unlike normal Lifetime)
```

**Use case:** Return when operation is cancelled before completion.

## Rules

1. **Listen on terminated = immediate callback**
   ```csharp
   var lt = new Lifetime();
   lt.Terminate();
   lt.Listen(() => Console.WriteLine("fires immediately!"));
   ```

2. **Child terminates with parent automatically**

3. **Single termination** - calling Terminate() twice is safe (no-op)

4. **Listeners cleared after termination** - prevents memory leaks

5. **Token is lazy** - CancellationTokenSource created only on first Token access

## Typical Patterns

### Scoped Subscription

```csharp
void Subscribe(IReadOnlyLifetime lifetime) {
    eventSource.Advise(lifetime, () => { /* handler */ });
    // Automatically unsubscribed when lifetime terminates
}
```

### Async Operation with Cancellation

```csharp
async Task ProcessLoop(IReadOnlyLifetime lifetime) {
    while (lifetime.IsTerminated == false) {
        await DoWork();
        await Task.Delay(1000, lifetime.Token);
    }
}
```

### Resource Cleanup

```csharp
lifetime.Listen(() => {
    connection.Dispose();
    timer.Stop();
    buffer.Clear();
});
```

### Conditional Child Lifetime

```csharp
if (condition) {
    var childLifetime = parentLifetime.Child();
    StartService(childLifetime);
    // Service stops when parent terminates OR childLifetime.Terminate()
}
```

## Key Files

| File | Purpose |
|------|---------|
| `Lifetimes/Abstract/IReadOnlyLifetime.cs` | Read-only interface |
| `Lifetimes/Abstract/ILifetime.cs` | Full interface |
| `Lifetimes/Lifetime.cs` | Main implementation |
| `Lifetimes/LifetimeExtensions.cs` | Child(), Intersect(), ToLifetime() |
| `Lifetimes/TerminatedLifetime.cs` | Pre-terminated singleton |

## Integration with Reactive

Lifetime is the foundation for all reactive types:

```
IReadOnlyLifetime
      |
      v
EventSource.Advise(lifetime, handler)  // auto-cleanup
ILifetimedValue.Advise(lifetime, handler)
IViewableList.Advise(lifetime, handler)
IViewableDictionary.Advise(lifetime, handler)
```

All reactive subscriptions use Lifetime for automatic cleanup.
