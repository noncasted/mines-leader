# Benchmark Framework

## Status: Done

## What's Done
- [x] BenchmarkResult model (POCO, not Orleans state — stored via direct SQL)
- [x] BenchmarkStorage — save/read results from PostgreSQL `benchmark_results` table
- [x] DB table setup in Aspire (BenchmarkSetup.cs)
- [x] DbLookup.Benchmark_Results constant
- [x] IClusterTest extended: MetricName, LastResult
- [x] ClusterTestRoot: Stopwatch timing, ReportMetric(), auto-save to BenchmarkStorage
- [x] Auto-fallback: if no ReportMetric() called, uses DurationMs
- [x] RunConcurrentIterations: auto-calculates ops/s and calls handle.ReportMetric()
- [x] ClusterTestNodeHandle.ReportMetric() proxies to root via callback
- [x] All 37 benchmark Root classes updated with BenchmarkStorage constructor + MetricName
- [x] Messaging benchmarks: manual Stopwatch + ReportMetric(totalMessages / elapsed) in all 6 files
- [x] State throughput benchmarks (9 files): auto ops/s via RunConcurrentIterations
- [x] State correctness benchmarks (11 files): MetricName = "ms", auto DurationMs fallback
- [x] Game/Meta/Infrastructure benchmarks: MetricName = "ms", auto DurationMs fallback
- [x] Console UI: Benchmarks page (route /benchmarks)
- [x] Console UI: BenchmarkEntry with metric display, delta vs previous, trend arrows
- [x] Console UI: BenchmarkHistory component — sparkline chart + history table
- [x] Navigation updated (Home, ConsoleConstants)

## Metric Strategy

| Group | MetricName | How it's reported |
|-------|-----------|------------------|
| State (throughput) | ops/s | Auto via RunConcurrentIterations |
| State (correctness) | ms | Auto fallback (DurationMs) |
| Messaging | msg/s | Manual Stopwatch in each Run() |
| Game | ms | Auto fallback (DurationMs) |
| Meta | ms | Auto fallback (DurationMs) |
| Infrastructure | ms | Auto fallback (DurationMs) |

## Architecture Decisions
- **SQL over Orleans State**: Benchmark results are NOT Orleans grain state. Using direct PostgreSQL 
  via IDbSource because we need aggregate queries (ORDER BY, GROUP BY, LIMIT) which Orleans state 
  doesn't support efficiently.
- **Auto-metric fallback**: If ReportMetric() is never called, ClusterTestRoot auto-reports DurationMs.
  This covers all "ms" benchmarks without any code in individual tests.
- **RunConcurrentIterations auto-reporting**: Calculates `iterations * concurrent / elapsed.TotalSeconds`
  and calls `handle.ReportMetric()` automatically.

## Ideas
- Export results as CSV/JSON
- Alerting on regression (> X% drop)
- Compare two specific runs side by side
- Baseline pinning — mark a run as "baseline" for comparison
- Group-level aggregate delta in headers
