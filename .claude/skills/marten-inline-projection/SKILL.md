# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /marten-inline-projection, marten inline projection, inline projection, мартен inline, адаптировать мартен, event store snapshot, martensnapshot, marten snapshot
BEHAVIOR: When triggered, execute the migration steps immediately. Do not explain — do.

# Marten Inline Projection Migration

Adapts the Atlantis event-store pattern to this project: Marten stores events in `mt_streams`/`mt_events`, and **inline projections** automatically maintain snapshot documents in `mt_doc_{Type}` tables. The state tables (`state_user_entity` etc.) are used only for **direct-state** grains, not event-sourced ones.

---

## Context: what Atlantis does

In `/atlantis/server/backend/Orchestration/Extensions/MartenExtensions.cs`:
- `opts.Events.StreamIdentity = StreamIdentity.AsString`
- `opts.Events.AppendMode = EventAppendMode.Quick`
- `opts.Projections.Snapshot<TDocument>(SnapshotLifecycle.Inline)`

**How it works there:**
1. `session.Events.Append(streamId, events)` writes to `mt_events`
2. `SnapshotLifecycle.Inline` — in the **same transaction** Marten rebuilds the aggregate via `Apply` and writes/updates the document in `mt_doc_{Type}`
3. `session.Load<TDocument>(streamId)` reads the snapshot from `mt_doc_*` — fast, no replay
4. `session.Query<TDocument>()` — for collections

---

## Step 1 — Fix Stream Key Format (CRITICAL)

**Problem:** `EventState.BuildStreamId()` returns `"user_entity:user/abc-def..."` where `user/` is the grain type from `GrainId.ToString()`. Marten `SetIdentityFromString` writes the full stream key into `Id`. `Guid.Parse("user/abc...")` crashes.

**Fix:** Stream key must contain only the raw key, no grain type.

| Was | Becomes |
|-----|---------|
| `user_entity:user/abc-def...` | `user_entity:abc-def...` |

**Files:**
- `backend/Infrastructure/Orleans/State/Events/EventState.cs` — `BuildStreamId()`: use `_context.GrainId.Key` (raw `IdSpan` value) instead of `_context.GrainId.ToString()`
- `backend/Infrastructure/Orleans/State/StateStorage.cs` — `Read()` for event-sourced: `identity.Key` already holds the clean key
- `backend/Infrastructure/Orleans/State/Events/EventStorage.cs` — `GetStreamIdsAsync()`: `LIKE` filter still works on `user_entity:%`

---

## Step 2 — Marten Inline Projections Setup

**Where:** `MartenSetupExtensions.cs` (wherever `IDocumentStore` is configured).

**Add:**
```csharp
// Event store
opts.Events.StreamIdentity = StreamIdentity.AsString;
opts.Events.AppendMode = EventAppendMode.Quick;

// Inline projections — Marten updates the document automatically when events are written
opts.Projections.Snapshot<UserState>(SnapshotLifecycle.Inline);
opts.Projections.Snapshot<MatchAggregate>(SnapshotLifecycle.Inline);
opts.Projections.Snapshot<UserAuthAggregate>(SnapshotLifecycle.Inline);
// ... ALL IEventStateValue types
```

**Result:** on `_eventStorage.Append(StreamId, events)`, Marten atomically writes:
- `mt_streams` + `mt_events` — the events
- `mt_doc_userstate` — the snapshot document

---

## Step 3 — EventState Cleanup

**Remove from `EventState<T>`:**
- `IStateStorage` dependency
- `_stateStorage.Write()` from `WriteSession()`
- `IGrainStateTransactionParticipant` (keep only `IGrainEventTransactionParticipant`)

**Leave:**
```csharp
public async Task Read()
{
    // Read snapshot from Marten (fast, no replay)
    _value = await _eventStorage.Load<TAggregate>(StreamId) ?? new TAggregate();
}

public async Task WriteSession()
{
    // Only events. Marten inline projection updates mt_doc_* in the same transaction.
    await _eventStorage.Append(StreamId, _pendingEvents.ToArray());
}

public IStateValue? GetAggregate() => _value; // for transactional collector
```

**Transactions:** `GrainTransactionHandler.CollectResult()` already collects `GetAggregate()` and adds it to `States`. `Transactions.Process()` writes `States` via `_stateStorage.Write()` — **but this is now unnecessary** because Marten itself updates the snapshot when writing events. Still, keep the path for backward compatibility until fully verified.

---

## Step 4 — EventStorage Refactoring

**`Read<T>` — read snapshot:**
```csharp
public async Task<T> Read<T>(string streamId) where T : class, new()
{
    await using var session = _store.QuerySession();
    return await session.LoadAsync<T>(streamId) ?? new T();
}
```

**`ReadAll<T>` — read all documents of a type:**
```csharp
public async IAsyncEnumerable<(string StreamId, T Aggregate)> ReadAll<T>(string streamPrefix)
    where T : class, new()
{
    await using var session = _store.QuerySession();
    var aggregates = await session.Query<T>()
        .Where(x => x.Id.StartsWith(streamPrefix))
        .ToListAsync();

    foreach (var aggregate in aggregates)
        yield return (aggregate.Id, aggregate);
}
```

**Remove:** `FixAggregateIdentity` — Marten inline projection manages `Id` itself.

---

## Step 5 — StateStorage — Remove Event-Sourced from Write and ReadAll

**`Write()`:**
```csharp
public async Task Write(StateWriteRequest request)
{
    // Only direct states. Event-sourced — Marten writes itself via Append.
    var directRecords = request.Records
        .Where(r => r.Value is IDirectStateValue)
        .ToDictionary();

    if (directRecords.Count != 0)
        await _directStorage.Write(new StateWriteRequest {
            Records = directRecords,
            Transaction = request.Transaction
        });
}
```

**`ReadAll()`:** for event-sourced — delegate to `EventStorage.ReadAll()`.

---

## Step 6 — StatesSetup — Separate Direct and Event-Sourced

**StatesSetup** creates tables only for `IDirectStateValue`:
- `state_test_collection`
- `state_simple`
- ...

**Do NOT create** tables for `IEventStateValue` — Marten manages `mt_doc_*` itself.

**StatesCleanup:**
```csharp
// Marten event store
await connection.Truncate("mt_streams");
await connection.Truncate("mt_events");
await connection.Truncate("mt_event_progression");

// Marten inline projection tables
await connection.Truncate("mt_doc_userstate");
await connection.Truncate("mt_doc_matchaggregate");
// ... all IEventStateValue snapshot tables
```

---

## Step 7 — Console / UI Adaptation

**Problem:** `UserState.Id` is now a `string` equal to the stream key. `PlayersWidget` does `Guid.Parse(user.Id)`.

**Solution:** `Id` contains the **full stream key** (`"user_entity:abc-def..."`). The UI works with `string`.

**Files to adapt:**
- `backend/Console/Home/PlayersWidget.razor` — `CopyId(Guid)` → `CopyId(string)`, `Guid.Parse(user.Id)` → keep as string
- `backend/Console/Game/User/UserDashboard.razor` — same
- `backend/Tools/Tests/Meta/UserGrainTests.cs` — assertions
- `backend/Tools/Tests/Meta/BotTests.cs` — assertions

**Grain collection calls** (`_collection.OnUpdatedTransactional(state.Id, state)`) must use the grain's primary key (`Guid`), not `state.Id`. Already fixed in previous commits.

---

## Step 8 — Tests

- `UserGrainTests` — `state!.Id` assertions must match stream key format
- `StateCollectionTests` — `ReadAll` reads from Marten `Query<>`, not from SQL
- Add test: after `grain.SetName()`, `session.Query<UserState>()` sees updated snapshot

---

## Execution Order

1. **Stream key format** (Step 1) — unblock all parsing
2. **Marten setup** (Step 2) — register inline projections
3. **EventState cleanup** (Step 3) — remove dual-write logic
4. **EventStorage refactoring** (Step 4) — Load/Query instead of AggregateStreamAsync
5. **StateStorage routing** (Step 5) — route event-sourced to EventStorage
6. **StatesSetup** (Step 6) — split direct/event-sourced tables
7. **UI adaptation** (Step 7) — fix Guid assumptions
8. **Tests** (Step 8) — verify snapshots work
