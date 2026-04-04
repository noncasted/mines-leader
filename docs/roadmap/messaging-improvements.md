# Messaging Infrastructure Improvements

## DurableQueue: Zero-Observer Delivery Loss

**Status:** TODO
**Priority:** High
**Component:** `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`

### Problem

`DurableQueue.Push()` silently succeeds when no observers are registered.
The message was already persisted via side effects (guaranteed write to DB),
but the delivery step is lost with no trace beyond a metric counter.

For a "durable" queue this is dangerous: the caller believes the message was
delivered (side effect marked as success), but no consumer ever received it.

### Current Behavior

```csharp
public async Task Push(object message) {
    BackendMetrics.DurableQueuePushed.Add(1);
    BackendMetrics.DurableQueueObserverCount.Record(_observers.Count);
    // If _observers is empty, nothing happens. No log, no error.
}
```

### Proposed Solution

Add explicit target handler declaration at queue registration time:

1. **Required observer registration** - when creating a DurableQueue consumer,
   declare expected observer count or named handlers
2. **Warning on zero observers** - log a warning when Push() is called with no
   observers, include queue name for diagnostics
3. **Dead letter mechanism** - if no observers are present, store the message in
   a dead letter table for later reprocessing
4. **Monitoring alert** - add a metric/alert when messages are pushed to
   observer-less queues beyond a threshold

### Impact

Without this fix, messages can be silently lost during:
- Deployment gaps (old silo down, new silo not yet subscribed)
- Configuration errors (wrong queue ID)
- Race conditions at startup (push before consumer registers)

### Related Files

- `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` - grain Push() method
- `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs` - client consumer management
- `backend/Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs` - side effect execution
