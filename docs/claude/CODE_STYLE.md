# Code Style Guide

## Member Organization

**Strict order of class members (MANDATORY):**

1. **Constructor**
2. **Private fields** (readonly fields first, then mutable)
3. **Public methods** (interface implementation)
4. **Private methods**
5. **Local functions** (inside methods)

**Example:**
```csharp
public class MessageQueueClient : IMessageQueueClient
{
    // 1. Constructor
    public MessageQueueClient(IOrleans orleans, ILogger<MessageQueueClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }
    
    
    // 2. Private fields (readonly grouped, then mutable)
    private readonly Dictionary<string, object> _delegates = new();
    private readonly ILogger<MessageQueueClient> _logger;
    private readonly Dictionary<IMessageQueueId, MessageQueueObserver> _observers = new();
    private readonly IOrleans _orleans;
    private readonly List<Func<Task>> _resubscribeActions = new();
    
    // 3. Public methods (interface)
    public Task Start(IReadOnlyLifetime lifetime) { ... }
    public IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageQueueId id) { ... }
    public Task PushTransactional(IMessageQueueId id, object message) { ... }
    public Task PushDirect(IMessageQueueId id, object message) { ... }
    
    // 4. Private methods (utilities)
    private IMessageQueue GetQueue(IMessageQueueId id) { ... }
    private async Task ResubscribeLoop(IReadOnlyLifetime lifetime) { ... }
}
```

## Field Naming

**Rules:**
- ✅ Private fields: `_camelCase` (leading underscore)
- ✅ Group readonly vs mutable fields
- ✅ Collection names are descriptive: `_observers`, `_delegates`, `_resubscribeActions`
- ✅ Dependency injections: `_logger`, `_orleans`
- ❌ Do NOT use `this.` unless necessary for clarity
- ❌ Do NOT abbreviate names: `_obs` instead of `_observers` is bad

**Bad naming example:**
```csharp
// ❌ Unclear purpose
private readonly Dictionary<IMessageQueueId, MessageQueueObserver> _o = new();
private readonly List<Func<Task>> _actions = new();

// ✅ Clear and explicit
private readonly Dictionary<IMessageQueueId, MessageQueueObserver> _observers = new();
private readonly List<Func<Task>> _resubscribeActions = new();
```

## Method Logic Organization

**Structure a method logically (example: GetOrCreateConsumer):**

1. **Fast path / existing check** - Return cached results immediately
2. **Creation** - Create new objects
3. **Setup** - Initialize and register dependencies
4. **Setup verification** - `GC.KeepAlive()` for objects that must persist
5. **Side effects** - Add to collections and register callbacks
6. **Return** - Return the result
7. **Local functions** - Helper functions at the end

**Example:**
```csharp
public IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageQueueId id)
{
    // 1. Fast path - already cached?
    var rawId = id.ToRaw();
    if (_delegates.TryGetValue(rawId, out var existing) == true)
        return (ViewableDelegate<T>)existing;

    // 2. Creation - instantiate new objects
    var source = new ViewableDelegate<T>();
    var observer = new MessageQueueObserver(message =>
        {
            if (message is not T castedMessage)
                throw new InvalidCastException();
            source.Invoke(castedMessage);
        }
    );

    // 3. Setup - initialize state
    _observers[id] = observer;
    var observerReference = _orleans.Client.CreateObjectReference<IMessageQueueObserver>(observer);

    // 4. Setup verification - prevent garbage collection
    GC.KeepAlive(observer);
    GC.KeepAlive(observerReference);

    // 5. Side effects - register callbacks
    Subscribe().NoAwait();
    _resubscribeActions.Add(Subscribe);
    _delegates[rawId] = source;

    // 6. Return
    return source;

    // 7. Local function - helper with closure over local variables
    Task Subscribe()
    {
        try
        {
            return GetQueue(id).AddObserver(observer.Id, observerReference);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
                rawId
            );
            return Task.CompletedTask;
        }
    }
}
```

## Local Functions

**When to use local functions:**
- ✅ Helper logic called 1-2 times within the method
- ✅ Logic that needs to capture method variables without parameters
- ✅ Error handlers or retry logic
- ✅ When extracting to a private method would obscure understanding

**Benefits:**
- Closure over outer method variables without passing parameters
- Context is immediately visible
- Doesn't pollute the class with private methods

**Example:**
```csharp
public async Task DoSomething()
{
    var data = GetData();

    // ✅ Local function captures 'data' without parameters
    Task Subscribe()
    {
        return _orleans.GetGrain<IQueue>(data.Id).Subscribe();
    }

    await Subscribe();
}
```

## Exception Handling in Local Functions

**Pattern: "safe subscribe"**

```csharp
Task Subscribe()
{
    try
    {
        return GetQueue(id).AddObserver(observer.Id, observerReference);
    }
    catch (Exception e)
    {
        _logger.LogError(
            e,
            "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
            rawId
        );
        return Task.CompletedTask;  // Graceful degradation
    }
}
```

**Why not throw:**
- Prevents application crash on resubscription failure
- Logging with context helps troubleshooting
- Returning `Task.CompletedTask` allows the application to continue

## Logging

**Log tags:**
- `[Messaging] [Queue]` - for MessageQueue and MessageQueueClient
- `[Messaging] [Pipe]` - for MessagePipe and MessagePipeClient

**Log message structure:**
```csharp
_logger.LogError(
    exception,                                    // exception (if any)
    "[Tag] [Component] Human readable message",   // template with tags and placeholders
    parameter                                     // parameters
);
```

**Example:**
```csharp
_logger.LogError(
    e,
    "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
    rawId
);
```

## Using .NoAwait()

**Rule:** When firing off a `Task` without awaiting in an async context, use `.NoAwait()`

**Example:**
```csharp
Subscribe().NoAwait();              // ✅ Explicitly shows intent to not await
_resubscribeActions.Add(Subscribe);

ResubscribeLoop(lifetime).NoAwait(); // ✅ Fire-and-forget
```

**Why it matters:**
- Explicitly signals intentional result discard
- Helps static analysis tools detect real issues
- Makes code intent clear to future developers

## Responsibility Distribution

**Single Responsibility Principle:**

Client-side class (MessageQueueClient) owns:
- ✅ Managing client-side Observers
- ✅ Resubscription on connection loss
- ✅ Converting messages to typed handlers
- ❌ NOT grain-side logic

Grain-side logic (MessageQueue grain) owns:
- ✅ Managing observer collection
- ✅ Broadcasting messages
- ✅ Grain lifetime management

## Collection Initialization

**Rule:** Initialize all collections inline with declaration (inline initializer)

```csharp
// ✅ Good - clearly empty on declaration
private readonly Dictionary<string, object> _delegates = new();

// ❌ Bad - have to search for initialization in constructor
private readonly Dictionary<string, object> _delegates;

public MyClass()
{
    _delegates = new Dictionary<string, object>();
}
```

## Property Initialization with `required` and `init`

**Rule:** If a field/property is initialized at object construction and never changes after, use `required` and `init` accessors

```csharp
// ✅ Good - field is set once at construction and is immutable
private class Entry
{
    public required IRoundAction Action { get; init; }
    public int RoundsLeft { get; set; }  // Mutable - no init
}

// Usage:
var entry = new Entry { Action = action, RoundsLeft = 5 };

// ❌ Bad - inconsistent initialization pattern
private class Entry
{
    public IRoundAction Action { get; set; }  // Can be changed after construction

    public Entry(IRoundAction action)
    {
        Action = action;
    }
}
```

**Benefits:**
- `required` - compiler ensures property is set during construction
- `init` - enforces immutability after object creation
- Clearer intent: this value won't change after initialization
- Type-safe initialization pattern

## Collection Modification During Iteration

**Rule:** Never modify a collection during iteration. Instead:
1. Collect items to remove in a separate list with their IDs
2. Iterate through the collection with forward iteration (not backwards)
3. Remove items after the iteration completes

```csharp
// ✅ Good - safe forward iteration with deferred removal
public async Task Tick(IReadOnlyLifetime lifetime)
{
    var toRemove = new List<Guid>();

    foreach (var entry in _scheduledActions)
    {
        entry.RoundsLeft--;

        if (entry.RoundsLeft == 0)
        {
            await entry.Action.Execute(lifetime);
            toRemove.Add(entry.Id);  // Collect IDs to remove
        }
    }

    foreach (var id in toRemove)
        _scheduledActions.RemoveAll(e => e.Id == id);  // Remove after iteration
}

// ❌ Bad - backward iteration complexity and unclear intent
public async Task Tick(IReadOnlyLifetime lifetime)
{
    for (var i = _scheduledActions.Count - 1; i >= 0; i--)
    {
        var entry = _scheduledActions[i];
        entry.RoundsLeft--;

        if (entry.RoundsLeft == 0)
        {
            await entry.Action.Execute(lifetime);
            _scheduledActions.RemoveAt(i);  // Direct removal during iteration
        }
    }
}
```

**Benefits:**
- Simpler forward iteration (more readable)
- Clearer separation of concerns (iteration vs removal)
- Easier to debug and understand intent
- ID-based removal is safer than index-based removal

## TryGetValue Pattern

**Use TryGetValue instead of Contains + Index:**

```csharp
// ✅ Good - single dictionary lookup
if (_delegates.TryGetValue(rawId, out var existing) == true)
    return (ViewableDelegate<T>)existing;

// ❌ Bad - two lookups (Contains + [])
if (_delegates.ContainsKey(rawId))
    return (ViewableDelegate<T>)_delegates[rawId];
```

## GC.KeepAlive() Usage

**Rule:** Use `GC.KeepAlive()` when an object must remain in memory despite having no active references

```csharp
// ✅ Correct - observer must persist for callbacks
GC.KeepAlive(observer);
GC.KeepAlive(observerReference);

// Without this, GC could collect the observer before it's needed
```

## Braces Placement (from .editorconfig)

**STRICT RULE - csharp_new_line_before_open_brace = none:**

```csharp
// ✅ Correct - opening brace on SAME line
public class MessageQueueClient : IMessageQueueClient {
    public Task Start(IReadOnlyLifetime lifetime) {
        // ...
    }

    private IMessageQueue GetQueue(IMessageQueueId id) {
        // ...
    }
}

// ❌ WRONG - brace on new line (violates EditorConfig)
public class MessageQueueClient : IMessageQueueClient
{
    public Task Start(IReadOnlyLifetime lifetime)
    {
        // ...
    }
}
```

## Complete Service Class Template

**Pattern for a service class:**

```csharp
public class MyService : IMyService
{
    // Public interface
    public Task DoSomething() { ... }
    public IObservable<T> GetStream<T>() { ... }

    // Private fields (readonly first, then mutable)
    private readonly ILogger<MyService> _logger;
    private readonly IOrleans _orleans;
    private readonly Dictionary<string, object> _state = new();

    // Constructor
    public MyService(ILogger<MyService> logger, IOrleans orleans)
    {
        _logger = logger;
        _orleans = orleans;
    }

    // Private methods
    private void ValidateInput(object input) { ... }
    private async Task InternalSetup() { ... }
}
```

## Summary

| Aspect | Rule |
|--------|------|
| Member order | Public → Private fields (readonly first) → Constructor → Private methods |
| Field names | `_camelCase`, descriptive, no abbreviations |
| Method logic | Fast-path → Create → Setup → Side effects → Return → Local functions |
| Local functions | For closures and context, placed at method end |
| GC.KeepAlive | When object must persist despite no references |
| Logging | With proper tags and structured format |
| Braces | ALWAYS on same line (EditorConfig requirement) |
| Error handling | Try-catch with graceful degradation and logging |
| Collections | Inline initialization with `= new()` |
| Dictionary lookup | Use `TryGetValue` not `Contains + []` |
