# Messaging Benchmarks — 2026-05-01

## Run Parameters

- **Date**: 2026-05-01
- **API**: `http://localhost:7103`
- **Group**: Messaging
- **Total benchmarks**: 10
- **Passed**: 9
- **Failed**: 1

---

## Results

| # | Benchmark | Subgroup | Result | Metric | Duration | Status |
|---|-----------|----------|--------|--------|----------|--------|
| 1 | Delivery throughput | DurableQueue | 735.55 | msg/s | 13 595 ms | OK |
| 2 | Direct push throughput | DurableQueue | 595.48 | msg/s | 16 793 ms | OK |
| 3 | Transactional push throughput | DurableQueue | 539.71 | msg/s | 18 528 ms | OK |
| 4 | Catch-up stress (disconnect/reconnect) | RuntimeChannel | 2056.96 | msg/s | 2 430 ms | OK |
| 5 | **Delivery timeout (slow observers)** | RuntimeChannel | **0.19** | **msg/s** | **5 217 ms** | **FAIL** |
| 6 | Distributed send throughput | RuntimeChannel | 9358.74 | msg/s | 40 069 ms | OK |
| 7 | Broadcast throughput | RuntimeChannel | 9307.07 | msg/s | 40 291 ms | OK |
| 8 | Retry stress (intermittent failures) | RuntimePipe | 8576.27 | req/s | 43 ms | OK |
| 9 | Request-response throughput | RuntimePipe | 24192.00 | msg/s | 5 580 ms | OK |
| 10 | StateCollection update throughput | DurableQueue | 921.46 | update/s | 5 426 ms | OK |

---

## Subsystem Breakdown

### RuntimePipe (in-memory, no persistence)

| Benchmark | Result | Metric | Notes |
|-----------|--------|--------|-------|
| Request-response throughput | 24 192.00 | msg/s | Highest throughput. No serialization or network overhead. |
| Retry stress (intermittent failures) | 8576.27 | req/s | Very short run (43 ms, 372 ops). Sample too small for reliable stats. |

**Observation**: RuntimePipe delivers the highest raw throughput. Retry stress benchmark is suspiciously short — likely under-configured for statistical significance.

### RuntimeChannel (in-memory broadcast within silo)

| Benchmark | Result | Metric | Notes |
|-----------|--------|--------|-------|
| Distributed send throughput | 9358.74 | msg/s | Stable batches ~7500 msg/800ms. |
| Broadcast throughput | 9307.07 | msg/s | Nearly identical to distributed send. Good symmetry. |
| Catch-up stress (disconnect/reconnect) | 2056.96 | msg/s | Reconnect scenario does not collapse throughput. Healthy. |
| **Delivery timeout (slow observers)** | **0.19** | **msg/s** | **FAIL — Orleans Request timeout (5s) < expected delivery timeout.** |

**Observation**: In-memory channel path is ~13x faster than DurableQueue. Catch-up stress proves resilience on reconnect. `Delivery timeout` failure is a configuration conflict, not a code regression.

### DurableQueue (PostgreSQL + jsonb persistence)

| Benchmark | Result | Metric | Notes |
|-----------|--------|--------|-------|
| Delivery throughput | 735.55 | msg/s | Baseline for durable delivery. |
| Direct push throughput | 595.48 | msg/s | Slightly lower than Delivery — different retry/dedup strategy. |
| Transactional push throughput | 539.71 | msg/s | Transactional overhead: −9.4% vs Direct push. |
| StateCollection update throughput | 921.46 | update/s | State path faster than queue path. Separate grain activation model. |

**Observation**: Persistent path is expectedly slower. Transactional overhead is reasonable (~9%). StateCollection updates outperform queue operations, suggesting different I/O batching.

---

## Transaction Overhead in DurableQueue

| Benchmark | Result | vs Direct push | Verdict |
|-----------|--------|---------------|---------|
| Direct push | 595.48 msg/s | baseline | — |
| Transactional push | 539.71 msg/s | −9.4% | Acceptable |

The transactional wrapper adds ~9% overhead. This is within expected bounds for Orleans transactions with PostgreSQL storage.

---

## Jitter & Sample Stability

### Distributed send / Broadcast

- Batches: ~7500 messages per 800 ms window.
- Coefficient of variation: low. Predictable throughput.
- Last batch in Distributed send spikes to 13124 msg/1352 ms — likely final flush or GC pause.

### Request-response

- Starts at ~2300 msg/100ms, drifts down to ~1900 msg/100ms by end of run.
- Warm-up drift: ~18%. Not alarming but worth monitoring over longer runs.

### StateCollection

- First 33 batches: 100 updates / ~105 ms (very stable).
- Last 17 batches: drift to 84–90 updates / ~104 ms.
- Possible causes: GC pressure, PostgreSQL connection pool saturation, or grain activation queue buildup on long runs.

---

## Failed Benchmark: Delivery timeout (slow observers)

**Error**: `Response did not arrive on time in 00:00:05 for message: Request [...] IRuntimeChannel.Publish(System.Object)`

**What happened**: Only 1 operation completed in 5217 ms. The Orleans Request timeout (default 5s) fired before the benchmark's simulated "slow observer" could finish processing.

**Root cause analysis**:
- This is a **configuration conflict**, not a performance regression.
- The benchmark expects a delivery timeout longer than the Orleans Request timeout.
- In a production cluster with tuned `ResponseTimeout`, this benchmark likely passes.
- Recent git history (last 10 commits) contains zero changes to messaging logic or Orleans timeout configuration — only infrastructure/compose changes.

**Recommendation**:
- Either increase `ResponseTimeout` for local Aspire runs, or
- Exclude this benchmark from local runs and run it only against a production-configured cluster, or
- Adjust the benchmark's internal timeout to stay within Orleans defaults.

---

## Historical Context

This is the **first recorded run** for all Messaging benchmarks in this environment. History arrays contain exactly one entry per benchmark. No trend analysis (regression / improvement / stability) is possible until at least 3–5 runs are collected.

---

## Recommendations

1. **Retry stress benchmark**: Increase `totalOperations` or duration. 372 ops in 43 ms is too short for meaningful variance analysis.
2. **Delivery timeout benchmark**: Fix configuration conflict with Orleans Request timeout before next run.
3. **StateCollection drift**: Monitor for GC or connection pool exhaustion on longer runs (>5000 ops).
4. **Collect more runs**: Re-run this group weekly to build trend history. Current dataset has N=1 for all benchmarks.
5. **Cross-group comparison**: Run `State` and `Infrastructure` groups to understand how Messaging fits into overall system throughput.

---

## Raw History JSON

Full history output was captured via:

```bash
curl -s http://localhost:7103/api/benchmarks/group/Messaging/history
```

The JSON contains per-sample batch data (count + timeMs) for each benchmark. It is available in the cluster's benchmark storage and can be re-fetched at any time.
