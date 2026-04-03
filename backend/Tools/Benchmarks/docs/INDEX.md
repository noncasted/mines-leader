# Benchmark Framework

## Architecture

```
IClusterTest (interface)
  -> ClusterTestRoot<TPayload> (base class, runs benchmark)
       -> Stopwatch timing
       -> ReportMetric(double) or auto-fallback to DurationMs
       -> BenchmarkStorage.Save(BenchmarkResult)
            -> PostgreSQL table: benchmark_results
```

## How It Works

1. Console UI injects `IEnumerable<IClusterTest>` — auto-discovered from Benchmarks assembly
2. User clicks Run — calls `IClusterTest.Start(IOperationProgress)`
3. `ClusterTestRoot.Start()` wraps `Run()` with Stopwatch, Lifetime, cleanup
4. Benchmark calls `ReportMetric(value)` or metric auto-calculates (see below)
5. Result saved to `benchmark_results` table via `BenchmarkStorage`
6. UI displays metric, delta vs previous run, history chart

## Metric Reporting

Three modes:

| Mode | How | When |
|------|-----|------|
| `RunConcurrentIterations` auto | `iterations * concurrent / elapsed.TotalSeconds` -> `handle.ReportMetric()` | State throughput benchmarks |
| Manual `ReportMetric()` | Benchmark calculates and calls `handle.ReportMetric(value)` | Messaging benchmarks (msg/s) |
| Auto fallback | If no `ReportMetric()` called, uses `stopwatch.ElapsedMilliseconds` | All "ms" benchmarks (Game, Meta, Infrastructure, State correctness) |

## Storage

- **Table**: `benchmark_results` (created by `Aspire/Startup/BenchmarkSetup.cs`)
- **Model**: `BenchmarkResult` in `Benchmarks/Common/BenchmarkResult.cs`
- **Service**: `BenchmarkStorage` in `Benchmarks/Common/BenchmarkStorage.cs`
- **Columns**: id, benchmark_name, group, metric_name, metric_value, duration_ms, payload_json, timestamp, success, error_message

## Key Classes

| Class | File | Role |
|-------|------|------|
| `IClusterTest` | `Common/IClusterTest.cs` | Interface: Group, Title, MetricName, Payload, Start() |
| `ClusterTestRoot<T>` | `Common/ClusterTestRoot.cs` | Base class: timing, metric save, cleanup |
| `ClusterTestNodeHandle` | `Common/ClusterTestNodeHandle.cs` | Passed to Run(): Progress, Lifetime, ReportMetric(), StartNode() |
| `ClusterTestNode<T>` | `Common/ClusterTestNode.cs` | Base for distributed worker nodes |
| `BenchmarkStorage` | `Common/BenchmarkStorage.cs` | Save/GetHistory/GetPrevious via SQL |
| `BenchmarkResult` | `Common/BenchmarkResult.cs` | Result model (POCO) |
| `TestsExtensions` | `TestsExtensions.cs` | RunConcurrentIterations with auto ops/s |
| `TestGroups` | `TestGroups.cs` | Group name constants |

## Benchmark Groups

| Group | Metric | Count | TODO | Doc |
|-------|--------|-------|------|-----|
| [State](STATE.md) | ops/s (throughput) or ms (correctness) | 20 | +9 | Grain state, transactions, migrations, storage |
| [Messaging](MESSAGING.md) | msg/s | 6 | +6 | Queues, pipes, channels |
| [Game](GAME.md) | ms | 5 | +13 | Board, cells, cards, commands, bots, round flow |
| [Meta](META.md) | ms | 3 | +10 | Users, auth, projection, matches, matchmaking, bots |
| [Infrastructure](INFRASTRUCTURE.md) | ops/s, ms | 13 | +2 | Task queue, balancer, reactive primitives, DB pool, side effects |

## Console UI

- **Route**: `/benchmarks`
- **Page**: `Console/Pages/Tests/Tests.razor`
- **Entry component**: `Console/Pages/Tests/TestEntry.razor` — metric display, delta %, trend arrows
- **History component**: `Console/Pages/Tests/BenchmarkHistory.razor` — sparkline chart + table
