# Orleans Transactions

ACID transactions across multiple grains. Ensures consistency for complex multi-grain updates.

## Transaction Basics

**Rule:** Every method that modifies `ITransactionalState` must have `[Transaction()]` attribute ONLY IN INTERFACE DEFINITION.

### Creating Transactions

```csharp
// Explicit transaction wrapper
await orleans.InTransaction(async () => {
    await user.SetName("NewName");      // Join Create transaction
    await deck.Initialize();            // Join Create transaction
    // Both succeed or both fail
});

// Result: Atomic - all operations commit or rollback together
```

### Transaction Isolation

```
Graph of grain calls:
  UserFactory.Create() [Transaction.Create]
       |
       +- User.Initialize() [Transaction.Join]
       |
       +- Deck.Initialize() [Transaction.Join]

Result: All execute in same ACID transaction
```

## Performance Principles

### Single Update Operation

```csharp
// GOOD - One atomic update
var newState = await _state.Update(state => {
    state.Field1 = value1;
    state.Field2 = value2;
    // All fields updated atomically
});

// BAD - Multiple updates cause race conditions
await _state.Update(state => state.Field1 = value1);
await _state.Update(state => state.Field2 = value2);
// Between updates, another grain call can read inconsistent state
```

**Rule:** Combine all updates into single `Update()` call to prevent race conditions.

### Parallel Independent Operations

```csharp
// GOOD - Task.WhenAll for independent operations
await Task.WhenAll(
    user.SetName("Name1"),
    deck.Initialize(),
    progression.AddRecord(record)
);

// Each grain operates independently (no dependencies)
// Parallel execution improves throughput
```

**Rule:** Use `Task.WhenAll` when operations don't depend on each other.

### Synchronous State Modifications

```csharp
// GOOD - Sync modifications in Update
var newState = await _state.Update(state => {
    // Synchronous operations only
    state.Value = CalculateNewValue();
    state.Count++;
    state.LastUpdate = DateTime.UtcNow;
    // No await allowed here
});

// BAD - Async inside Update callback
var newState = await _state.Update(async state => {  // ERROR: Can't be async
    await SomeAsync();  // NOT ALLOWED
});
```

**Rule:** `Update()` callback is synchronous. Async operations happen after `Update()` completes.

### Pattern: Read, Calculate, Update, Act

```csharp
[Transaction(TransactionOption.Join)]
public async Task ComplexOperation()
{
    // 1. Read current state synchronously
    var value = await _state.PerformRead(state => state.Value);

    // 2. Calculate outside transaction
    var newValue = CalculateAsync(value);

    // 3. Update in single operation
    var updatedState = await _state.Update(state => {
        state.Value = newValue;
    });

    // 4. Side effects after state is committed
    await SendProjection(updatedState);
    await LogChange(updatedState);
}
```

**Benefits:**
- Atomic state modifications
- Async calculations outside transaction scope
- Side effects only after commit

## Transaction Scope

### Intra-Grain Transactions

```csharp
[Transaction(TransactionOption.Join)]
public async Task UpdateThenNotify()
{
    // Update state within transaction
    var newState = await _state.Update(state => {
        state.Name = newName;
    });

    // Side effects happen after transaction
    await projection.SendCached(newState);
}
```

### Multi-Grain Transactions

```csharp
public async Task CreateUserWithDeck(Guid userId)
{
    await _orleans.InTransaction(async () => {
        var user = _orleans.GetGrain<IUser>(userId);
        var deck = _orleans.GetGrain<IUserDeck>(userId);

        // Both calls participate in same transaction
        await user.Initialize();
        await deck.Initialize();
    });
}

// Guarantees:
// - Both grains initialize
// - Both state changes commit atomically
// - If user initialize fails, deck initialize rolls back
```

## Transactional State Hook

### BatchWriter Transaction Integration

```csharp
public class MessageQueue : BatchWriter<MessageQueueState, object>
{
    // Automatically called on transaction commit
    public Task OnSuccess(Guid transactionId)
    {
        // Move entries from pending to state
        _state.State.Entries.AddRange(_pending[transactionId]);
        _pending.Remove(transactionId);
        return _state.WriteStateAsync();
    }

    // Automatically called on transaction rollback
    public Task OnFailure(Guid transactionId)
    {
        // Clean up pending entries
        _pending.Remove(transactionId);
        return Task.CompletedTask;
    }
}

// Usage in transaction
await messaging.PushTransactionalQueue(queueId, message);
// Message only added to state on transaction commit
```

## Error Handling

### Automatic Rollback

```csharp
[Transaction(TransactionOption.Join)]
public async Task OperationWithError()
{
    var state = await _state.Update(state => {
        state.Value = newValue;  // Updates in-memory
    });

    if (ValidateState(state) == false)
    {
        throw new InvalidOperationException("State invalid");
    }

    // If error thrown before commit:
    // - State changes are rolled back
    // - Batch entries not committed
    // - Caller receives exception
}
```

**Rule:** Orleans automatically rolls back on any exception. No manual cleanup needed.

### Nested Transactions Not Allowed

```csharp
[Transaction(TransactionOption.Create)]
public async Task Outer()
{
    await Inner();  // ERROR: Already in transaction
}

[Transaction(TransactionOption.Create)]
public async Task Inner()
{
    // Can't start new transaction inside existing
}

// CORRECT:
[Transaction(TransactionOption.Join)]  // Join existing, don't create
public async Task Inner() { }
```

## Critical Rules

1. **Always mark state-modifying methods with [Transaction()]** - Ensures ACID
2. **One Update() per transaction** - Multiple updates = race conditions
3. **Sync-only in Update callback** - No async inside state modification
4. **Use Task.WhenAll for independent operations** - Improves throughput
5. **Read before transaction scope** - Use PerformRead for initial values
6. **Side effects after Update()** - Projections, logging, etc after commit
7. **Transactional vs Direct messaging** - Know when to use which
8. **Automatic rollback** - Failed transactions don't need cleanup
9. **No nested Creates** - Use TransactionOption.Join for chained calls

## Messaging in Transactions

### Transactional Queue

```csharp
// Within Orleans transaction
await messaging.PushTransactionalQueue(queueId, message);

// Behavior:
// - Message added to pending on call
// - On transaction success: moves to state and processes
// - On transaction failure: removed from pending
```

### Direct Queue

```csharp
// Immediate send, not transactional
await messaging.PushDirectQueue(queueId, message);

// Behavior:
// - Message sent immediately
// - No transaction dependency
```

## Common Patterns

### Pattern 1: Create Atomic Entity

```csharp
[Transaction(TransactionOption.Create)]
public async Task CreateUser(string name)
{
    var userId = Guid.NewGuid();
    var user = _orleans.GetGrain<IUser>(userId);
    var deck = _orleans.GetGrain<IUserDeck>(userId);

    await Task.WhenAll(
        user.Initialize(),
        deck.Initialize()
    );

    return userId;
}
```

### Pattern 2: Update with Side Effects

```csharp
[Transaction(TransactionOption.Join)]
public async Task SetName(string newName)
{
    var newState = await _state.Update(state => {
        state.Name = newName;
    });

    // After transaction commit
    await this.SendCachedProjection(newState);
}
```

### Pattern 3: Match Completion (Multi-Grain Atomic)

```csharp
[Transaction(TransactionOption.Join)]
public async Task OnComplete(Guid winnerId)
{
    // Atomic state update
    var matchState = await _state.Update(state => {
        state.Winner = winnerId;
        state.Time = DateTime.UtcNow - state.StartDate;
    });

    // After commit: Update both players (independent)
    var loser = participants.First(p => p != winnerId);
    await Task.WhenAll(
        UpdateWinner(winnerId, matchState),
        UpdateLoser(loser, matchState)
    );
}

private async Task UpdateWinner(Guid userId, MatchState match)
{
    var handle = _orleans.CreateUserHandle(userId);
    await Task.WhenAll(
        handle.MatchHistory.Add(match.ToOverview()),
        handle.Progression.AddRecord(new WinRecord())
    );
}
```

## Logging

| Tag | Component | Usage |
|-----|-----------|-------|
| `[Transaction]` | Transaction system | Transaction start/commit/rollback |
| `[Orleans]` | Orleans grains | State operations |

## Key Files

| File | Purpose |
|------|---------|
| `Common/Orleans/Extensions/OrleansExtensions.cs` | Transaction helpers |
| `Infrastructure/Orleans/Silo/Program.cs` | Transaction configuration |
| `Infrastructure/StorableActions/Batchers/Grains/BatchWriter.cs` | Transaction hook example |

## Integration with Other Systems

- **Messaging** - Transactional vs Direct queue selection critical
- **Projections** - State changes sent to client after transaction commit
- **BatchWriter** - Transaction hooks ensure exactly-once semantics
- **Service Discovery** - Transaction spans before service calls