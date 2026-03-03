# Copilot Code Style Guide - Method Level

This guide covers **single-method completion patterns** for AI code completion. Focus on what happens *inside* methods, not class structure.

---

## 1. Comparisons - Always Full Body

**RULE:** Never use implicit boolean checks. Always compare with full statement.

```csharp
// ✅ Good - explicit comparison
if (dictionary.TryGetValue(id, out var value) == true)
    return value;

if (condition == false)
    throw new InvalidOperationException();

if (count > 0)
    await DoSomething();

// ❌ Bad - implicit boolean
if (dictionary.TryGetValue(id, out var value))  // Implicit true
    return value;

if (!condition)                                   // Implicit negation
    throw new InvalidOperationException();

if (items.Any())                                  // Implicit true
    process;
```

**Pattern:** Use `== true`, `== false`, relational operators (`>`, `<`, `==`), but never bare boolean or `!` prefix.

---

## 2. Collection Initialization - Prefer `= new()`

**RULE:** Inline initialize collections with `= new()` not `new Type()`.

```csharp
// ✅ Good - concise, clear
private List<Item> _items = new();
private Dictionary<string, Handler> _handlers = new();

var tempList = new List<string>();  // Local variable
var tempDict = new Dictionary<int, object>();

// ❌ Bad - redundant type specification
private readonly List<Item> _items = new List<Item>();
private readonly Dictionary<string, Handler> _handlers = new Dictionary<string, Handler>();
```

---

## 3. Manual Loop Iteration - Avoid LINQ for Iteration

**RULE:** Use `foreach` or `for` loops for iteration. Reserve LINQ for projections/filtering in assignments.

```csharp
// ✅ Good - explicit iteration with collection building
var results = new List<string>();
foreach (var item in _items) {
    if (item.IsActive)
        results.Add(item.Name);
}
return results;

// ✅ Good - LINQ for projection/filtering in return
return _items.Where(i => i.IsActive).Select(i => i.Name).ToList();

// ❌ Bad - side effects in LINQ
_items.ForEach(item => {
    item.Process();
    _results.Add(item);
});

_items.Where(i => i.IsActive).ForEach(i => _results.Add(i));
```

**Exception:** LINQ is fine for `.Select()`, `.Where()`, `.FirstOrDefault()` in direct assignments.

---

## 4. Collection Modification During Iteration

**RULE:** Never modify during iteration. Collect IDs, iterate, then remove.

```csharp
// ✅ Good - safe forward iteration
public async Task ProcessEntries(IReadOnlyLifetime lifetime) {
    var toRemove = new List<Guid>();

    foreach (var entry in _entries) {
        entry.Counter--;

        if (entry.Counter == 0) {
            await entry.Action.Execute(lifetime);
            toRemove.Add(entry.Id);
        }
    }

    foreach (var id in toRemove)
        _entries.RemoveAll(e => e.Id == id);
}

// ❌ Bad - backward iteration or direct removal
for (var i = _entries.Count - 1; i >= 0; i--) {
    _entries.RemoveAt(i);  // Direct removal
}
```

---

## 5. Dictionary Lookups - Use TryGetValue

**RULE:** One lookup with `TryGetValue`, not two with `Contains + []`.

```csharp
// ✅ Good - single lookup
if (_cache.TryGetValue(key, out var cached) == true)
    return cached;

// ❌ Bad - two lookups
if (_cache.ContainsKey(key))
    return _cache[key];

// Also avoid
var value = _cache[key];  // Throws if missing
```

---

## 6. Fire-and-Forget Tasks - Use .NoAwait()

**RULE:** When discarding a Task, use `.NoAwait()` extension to signal intent.

```csharp
// ✅ Good - explicit "fire and forget"
ResubscribeLoop(lifetime).NoAwait();
_callbacks.Add(Subscribe);

Subscribe().NoAwait();

// ❌ Bad - unclear intent
_ = ResubscribeLoop(lifetime);

ResubscribeLoop(lifetime);  // Result ignored, not obvious
```

---

## 7. Local Functions - For Closures & Context

**RULE:** Use local functions when logic captures outer variables or is called 1-2 times.

```csharp
// ✅ Good - local function captures 'lifetime' and '_logger'
public async Task Setup() {
    var isReady = await ValidateAsync();

    Task Subscribe() {
        try {
            return _service.Register(isReady);
        } catch (Exception e) {
            _logger.LogError(e, "Failed to subscribe");
            return Task.CompletedTask;
        }
    }

    await Subscribe();
}

// ❌ Bad - extract to private method if called many times
private async Task Subscribe() { ... }

// ❌ Bad - don't use for one-off simple logic
Action validate = () => { return true; };
```

---

## 8. Exception Handling - Graceful Degradation

**RULE:** Catch and log, return safe default. Don't let subscriptions fail silently.

```csharp
// ✅ Good - log and return safe state
Task Subscribe() {
    try {
        return GetQueue(id).AddObserver(observer.Id, reference);
    } catch (Exception e) {
        _logger.LogError(e, "[Messaging] [Queue] Failed to rebind observer {QueueId}", id);
        return Task.CompletedTask;  // Continue execution
    }
}

// ❌ Bad - silent failure
try {
    await Subscribe();
} catch { }  // Swallowed, no logging

// ❌ Bad - immediate throw in background task
Subscribe().NoAwait();  // Will crash if exception
```

---

## 9. Naming - Descriptive and No Abbreviations

**RULE:** Variable and field names should be complete words. No single-letter abbreviations.

```csharp
// ✅ Good - clear intent
var observers = GetAllObservers();
var resubscribeActions = new List<Func<Task>>();
var messageQueueId = request.Id;

foreach (var entry in _scheduledActions) {
    entry.RoundsLeft--;
}

// ❌ Bad - unclear abbreviations
var obs = GetAllObservers();
var subActions = new List<Func<Task>>();
var qId = request.Id;

foreach (var e in _actions) {
    e.Remaining--;
}
```

---

## 10. GC.KeepAlive() - Pin Objects to Memory

**RULE:** Use when an object has no logical references but must persist (event handlers, Orleans references).

```csharp
// ✅ Good - keep observer alive for callbacks
var observer = new MessageQueueObserver(message => {
    source.Invoke(message);
});
var reference = _orleans.Client.CreateObjectReference(observer);

GC.KeepAlive(observer);      // Prevent GC until this point
GC.KeepAlive(reference);

// ❌ Bad - object gets garbage collected before use
var observer = new MessageQueueObserver(...);
// ... observer is now eligible for GC if no local references remain
```

---

## 11. Control Flow - Early Returns

**RULE:** Exit early for validation/cache hits. Reduce nesting.

```csharp
// ✅ Good - fast path first
public Result Process(Request request) {
    if (request == null)
        throw new ArgumentNullException(nameof(request));

    if (_cache.TryGetValue(request.Id, out var cached) == true)
        return cached;

    // Main logic at one level
    var result = ComputeResult(request);
    _cache[request.Id] = result;
    return result;
}

// ❌ Bad - nested conditions
public Result Process(Request request) {
    if (request != null) {
        if (!_cache.TryGetValue(request.Id, out var cached)) {
            var result = ComputeResult(request);
            _cache[request.Id] = result;
            return result;
        } else {
            return cached;
        }
    } else {
        throw new ArgumentNullException(nameof(request));
    }
}
```

---

## 12. String Patterns - String Interpolation

**RULE:** Use string interpolation `$"{variable}"` not `string.Format()` or concatenation.

```csharp
// ✅ Good - modern, readable
_logger.LogError(e, "[Service] Failed to process {UserId} at {Timestamp}",
    userId, DateTime.Now);

var message = $"User {userId} processed in {elapsed}ms";

// ❌ Bad - outdated or hard to read
_logger.LogError(e, string.Format("[Service] Failed to process {0}", userId));

var message = "User " + userId + " processed in " + elapsed + "ms";
```

---

## Quick Checklist for Completions

When Copilot generates method code, verify:

- [ ] Comparisons use full body (`== true`, `== false`, `>`, `<`, not `if (bool)`)
- [ ] Collections initialized as `= new()` not `new Type()`
- [ ] Loops are `foreach`/`for`, not `.ForEach()` or `.Where(...).ForEach(...)`
- [ ] Dictionary lookup uses `TryGetValue`, not `Contains + []`
- [ ] Fire-and-forget tasks use `.NoAwait()`
- [ ] Local functions capture closure context, placed at method end
- [ ] Exception handling includes logging
- [ ] Variable names are complete, no abbreviations
- [ ] Early returns minimize nesting
- [ ] String interpolation with `$"..."`
