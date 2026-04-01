# Infrastructure Benchmarks

## Overview

Tests task scheduling infrastructure, reactive primitives, DB connection pool, and side effect execution. Covers TaskQueue operations, TaskBalancer behavior, EventSource/Lifetime/ViewableDictionary throughput, connection pool throughput, and SideEffectsWorker transactional/retry paths.

## Metrics

- `ops/s` — reactive primitives and DB pool (auto via `RunConcurrentIterations` or manual `ReportMetric`)
- `ms` — task scheduling and side effect benchmarks (auto-fallback to `DurationMs`)

---

## Benchmarks

### task-queue-collect
- **File**: `backend/Benchmarks/Infrastructure/TaskQueueCollectTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: TaskQueue collect operation. Enqueues 2 tasks, verifies `Collect()` returns both, second `Collect()` returns empty (tasks consumed).
- **Distributed**: No

### task-queue-deduplication
- **File**: `backend/Benchmarks/Infrastructure/TaskQueueDeduplicationTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: TaskQueue deduplication. Enqueues 2 tasks with same ID but different priorities, verifies only first is kept with original priority.
- **Distributed**: No

### task-queue-delay
- **File**: `backend/Benchmarks/Infrastructure/TaskQueueDelayTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: TaskQueue delay handling. Enqueues immediate and 60s-delayed tasks, verifies only immediate task collected.
- **Distributed**: No

### task-balancer-priority
- **File**: `backend/Benchmarks/Infrastructure/TaskBalancerPriorityTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: TaskBalancer priority ordering. Enqueues Low, Critical, Medium tasks in sequential mode, verifies execution order: Critical, Medium, Low.
- **Distributed**: No

### task-balancer-exception-penalty
- **File**: `backend/Benchmarks/Infrastructure/TaskBalancerExceptionPenaltyTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: TaskBalancer exception penalty. Failing high-priority task gets penalized and re-scheduled after normal task, then retries and succeeds.
- **Distributed**: No

### task-balancer-concurrency
- **File**: `backend/Benchmarks/Infrastructure/TaskBalancerConcurrencyTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: TaskBalancer concurrency limit. 6 slow tasks (200ms each) with max 2 concurrent, verifies max active never exceeds 2 and all complete.
- **Distributed**: No

### reactive-event-fanout
- **File**: `backend/Benchmarks/Infrastructure/ReactiveEventFanoutTest.cs`
- **Payload**: Iterations=100, Concurrent=10, SubscriberCount=50
- **What it measures**: EventSource.Invoke() fan-out throughput. Creates EventSource<int> with N subscribers, measures invocations/second. Pure in-memory.
- **Distributed**: No

### reactive-lifetime-teardown
- **File**: `backend/Benchmarks/Infrastructure/ReactiveLifetimeTeardownTest.cs`
- **Payload**: ListenerCount=100, Iterations=1000
- **What it measures**: Lifetime.Terminate() bulk cleanup cost. Creates Lifetime with N listeners, terminates, measures teardowns/second. Simulates session shutdown.
- **Distributed**: No

### reactive-viewable-dict
- **File**: `backend/Benchmarks/Infrastructure/ReactiveViewableDictTest.cs`
- **Payload**: SubscriberCount=10, Iterations=1000
- **What it measures**: ViewableDictionary Add/Remove cycle with N subscribers. Each iteration creates entry Lifetime + fires EventSource. Measures ops/second.
- **Distributed**: No

### db-connection-pool
- **File**: `backend/Benchmarks/Infrastructure/DbConnectionPoolTest.cs`
- **Payload**: Iterations=100, Concurrent=10
- **What it measures**: Raw Npgsql connection pool acquisition throughput via DbSource.OpenConnection(). Foundational cost for all DB operations.
- **Distributed**: No

### side-effect-transactional
- **File**: `backend/Benchmarks/Infrastructure/SideEffectTransactionalTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: SideEffectsWorker with ITransactionalSideEffect. Registers transactional side effect, waits for execution via transactional path.
- **Distributed**: No

### side-effect-concurrent-slots
- **File**: `backend/Benchmarks/Infrastructure/SideEffectConcurrentSlotsTest.cs`
- **Payload**: EffectCount=20
- **What it measures**: SideEffectsWorker under concurrent slot saturation. Registers N side effects simultaneously, measures time until all complete.
- **Distributed**: No

### side-effect-retry-cycle
- **File**: `backend/Benchmarks/Infrastructure/SideEffectRetryCycleTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: SideEffectsWorker retry cycle. Side effect fails on first attempt, waits for retry delay + re-execution. Measures full fail-requeue-execute cycle.
- **Distributed**: No

---

## TODO

### Correctness (ms)
- [ ] **service-loop-startup** — `ServiceLoop` stage fan-out: time for all `IOrleansStarted` handlers to complete with 5/10/20 registered participants.
- [ ] **db-connection-exhaustion** — `DbSource.OpenConnection()` behaviour under pool exhaustion (N=max+10 concurrent opens). Measures wait time and recovery.

---

## Helper Files (not benchmarks)

- `TaskSchedulingTestHelpers.cs` — `TestPriorityTask`, `TestBalancerConfig` helper classes used by balancer benchmarks
