# State Benchmarks

## Overview

Tests Orleans grain state operations: read/write cycles, transactions (single, chained, overlapping, concurrent), state migration between schema versions, persistence to PostgreSQL, side effect execution, and StateCollection sync across services.

## Metric

- **Throughput benchmarks** (14): `ops/s` — auto-calculated by `RunConcurrentIterations` as `iterations * concurrent / elapsed.TotalSeconds`
- **Correctness benchmarks** (11): `ms` — auto-fallback to `DurationMs`

---

## Throughput Benchmarks (ops/s)

### state
- **File**: `backend/Benchmarks/State/StateTest.cs`
- **Payload**: `Iterations=100`, `Concurrent=10`
- **What it measures**: Basic grain state read/write cycle. Each operation creates a grain, reads state, modifies fields, writes back.
- **Distributed**: No

### transactions-state
- **File**: `backend/Benchmarks/State/TransactionStateTest.cs`
- **Payload**: `Iterations=10`, `Concurrent=3`
- **What it measures**: Single-grain transactional increment. Each operation runs `Increment()` inside a transaction on a unique grain.
- **Distributed**: No

### transactions-state-chained
- **File**: `backend/Benchmarks/State/TransactionStateChainedTest.cs`
- **Payload**: `ChainLength=3`, `Iterations=100`, `Concurrent=3`
- **What it measures**: Chained transaction across N grains. Single transaction increments all grains in sequence.
- **Distributed**: No

### transactions-state-chained-fail
- **File**: `backend/Benchmarks/State/TransactionStateChainedFailTest.cs`
- **Payload**: `ChainLength=3`, `Iterations=100`, `Concurrent=3`
- **What it measures**: Transaction rollback correctness. Increments all grains then throws — verifies all roll back to initial values.
- **Distributed**: No

### transactions-state-overlapping
- **File**: `backend/Benchmarks/State/TransactionStateOverlappingTest.cs`
- **Payload**: `ChainLength=3`, `Iterations=100`, `Concurrent=3`
- **What it measures**: Two overlapping transactions on the same grain chain executed concurrently. Both must succeed.
- **Distributed**: No

### transactions-single-chain
- **File**: `backend/Benchmarks/State/TransactionSingleTargetTest.cs`
- **Payload**: `Iterations=10`, `Concurrent=3`
- **What it measures**: Contention on a single grain. Multiple concurrent transactions increment the same grain ID.
- **Distributed**: No

### transactions-single-target
- **File**: `backend/Benchmarks/State/TransactionSingleChainTest.cs`
- **Payload**: `Iterations=10`, `Concurrent=3`, `ChainLength=3`
- **What it measures**: Contention on a fixed chain. Concurrent transactions all increment the same set of grains.
- **Distributed**: No

### transactions-cross-path-read
- **File**: `backend/Benchmarks/State/TransactionCrossPathReadTest.cs`
- **Payload**: `Iterations=50`, `Concurrent=3`
- **What it measures**: Write via transaction, then read via non-transactional path. Verifies committed values are visible outside transactions.
- **Distributed**: No

### state-migration-concurrent
- **File**: `backend/Benchmarks/State/StateMigrationConcurrentTest.cs`
- **Payload**: `Iterations=20`, `Concurrent=5`
- **What it measures**: Concurrent state migrations from V0 to V1 schema. Writes as V0 grain, reads as V1 grain triggering migration.
- **Distributed**: No

### event-storage
- **File**: `backend/Benchmarks/State/EventStorageTest.cs`
- **Payload**: `Iterations=3300`, `Concurrent=10`
- **What it measures**: Direct `IEventStorage` read + append through a grain. Each operation creates a grain, reads aggregate via `EventStorage.Read<>()`, appends an event via `EventStorage.Append()`.
- **Distributed**: No

### event-state
- **File**: `backend/Benchmarks/State/EventStateTest.cs`
- **Payload**: `Iterations=3300`, `Concurrent=10`
- **What it measures**: Event-sourced grain read/append/write cycle through `EventState<TAggregate>`. Each operation creates a grain, loads aggregate, appends an event, writes pending events.
- **Distributed**: No

### event-state-transaction
- **File**: `backend/Benchmarks/State/EventStateTransactionTest.cs`
- **Payload**: `Iterations=3300`, `Concurrent=3`
- **What it measures**: Single-grain transactional event append. Each operation runs `Append()` inside a transaction on a unique event-sourced grain.
- **Distributed**: No

### event-state-transaction-chained
- **File**: `backend/Benchmarks/State/EventStateTransactionChainedTest.cs`
- **Payload**: `ChainLength=3`, `Iterations=1250`, `Concurrent=3`
- **What it measures**: Chained transaction across N event-sourced grains. Single transaction appends events to all grains in sequence.
- **Distributed**: No

### event-state-transaction-concurrent
- **File**: `backend/Benchmarks/State/EventStateTransactionConcurrentTest.cs`
- **Payload**: `ConcurrentTransactions=10`, `Iterations=3300`, `Concurrent=3`
- **What it measures**: Concurrent transactions on a single event-sourced grain. Multiple transactions append events to the same grain concurrently.
- **Distributed**: No

---

## Correctness Benchmarks (ms)

### state-migration
- **File**: `backend/Benchmarks/State/StateMigrationTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Single state migration V0 to V1. Writes value, reads as V1 grain, verifies migrated fields.
- **Distributed**: No

### state-persistence
- **File**: `backend/Benchmarks/State/StatePersistenceTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: State persistence to PostgreSQL. Writes value, deactivates grain, reads from fresh activation to verify database round-trip.
- **Distributed**: No

### state-collection-sync
- **File**: `backend/Benchmarks/State/StateCollectionSyncTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: StateCollection sync across cluster services via durable queue propagation. Writes via transaction, verifies local and remote collection views.
- **Distributed**: Yes

### transactions-empty
- **File**: `backend/Benchmarks/State/TransactionEmptyTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Empty transaction overhead. Runs transaction with no grain calls.
- **Distributed**: No

### transactions-rollback-retry
- **File**: `backend/Benchmarks/State/TransactionRollbackRetryTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Rollback then immediate retry. Failed transaction rolls back, retry succeeds without waiting for takeover.
- **Distributed**: No

### transactions-mid-chain-fail
- **File**: `backend/Benchmarks/State/TransactionMidChainFailTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Mid-chain failure rollback. Increments grain A, increments grain B, throws — verifies both roll back.
- **Distributed**: No

### transactions-takeover
- **File**: `backend/Benchmarks/State/TransactionTakeoverTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Lock takeover after 30s threshold. Slow 40s transaction holds lock, fast transaction waits then takes over.
- **Distributed**: No

### transactions-large-batch
- **File**: `backend/Benchmarks/State/TransactionLargeBatchTest.cs`
- **Payload**: `GrainCount=50`
- **What it measures**: Single transaction spanning N grains. Verifies all grains incremented after commit.
- **Distributed**: No

### transactions-state-value
- **File**: `backend/Benchmarks/State/TransactionStateValueTest.cs`
- **Payload**: `TransactionCount=50`
- **What it measures**: Sequential transactions on same grain. N transactions each increment, verifies no lost updates.
- **Distributed**: No

### transactions-concurrent-value
- **File**: `backend/Benchmarks/State/TransactionConcurrentValueTest.cs`
- **Payload**: `ConcurrentTransactions=10`
- **What it measures**: Concurrent transactions on same grain. N concurrent increments, verifies final value equals successful transaction count.
- **Distributed**: No

### side-effect-execution
- **File**: `backend/Benchmarks/State/SideEffectExecutionTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Side effect execution delay. Registers side effect in transaction, polls for SideEffectsWorker to execute it (up to 30s).
- **Distributed**: No

---

---

## TODO

### Throughput (ops/s)
- [ ] **state-storage-direct-read** — `StateStorage.ReadRaw()` direct DB round-trip without Orleans grain layer overhead. Isolates raw storage latency.
- [ ] **state-storage-batch-read** — `StateStorage.Read<TKey,TValue>(IReadOnlyList<StateIdentity>)` batch read at N=10/50/100 keys. Compare vs N individual reads.
- [ ] **state-storage-batch-write** — `StateStorage.Write(NpgsqlTransaction, dict)` multi-record transactional batch write at N=10/50/100.
- [ ] **state-collection-population** — `StateCollection` load from DB via `StateCollectionUtils.Load()` with 100/1000/10000 rows. Measures startup cost.

### Correctness (ms)
- [ ] **state-storage-read-all** — `StateStorage.ReadAll<TKey,TValue>()` streaming async enumerable over entire table at 100/1000/10000 rows.
- [ ] **state-storage-delete-batch** — `StateStorage.Delete()` batch deletion, verify cleanup.
- [ ] **addressable-state-propagation** — `AddressableState.SetValue()` cross-node propagation latency: time from SetValue on silo-A to ViewableProperty.Set firing on silo-B.
- [ ] **dynamic-state-propagation** — `DynamicState.SetValue()` in-process set + RuntimeChannel publish. Compare with AddressableState.
- [ ] **addressable-state-concurrent-write** — Multiple concurrent `SetValue()` calls on same AddressableState, verify write-lock serialisation correctness.

---

## Helper Files (not benchmarks)

- `TransactionTestsGrain.cs` — `ITransactionTestGrain`, `TransactionTestState`, `TransactionTestGrain` used by transaction benchmarks
- `EventBenchGrains.cs` — `EventBenchAggregate`, `CounterIncremented`, `IEventStorageTestGrain`, `IEventStateTestGrain`, `IEventStateTransactionTestGrain` and their grain implementations used by event-sourced benchmarks
