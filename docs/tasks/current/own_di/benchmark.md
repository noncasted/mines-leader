# Own DI vs VContainer — benchmark

Status: harness has three columns. VContainer and runtime-plan (Own) measured in the same Editor session. **Generated class has no numbers** — waits steps 1, 1c and 2. **No winner is claimed.** The generated column is empty, so this is not a complete step-3 perf report.

| Field | Value |
|---|---|
| Date measured | 2026-09-10 05:20:59 UTC (Own), 2026-09-10 05:21:09 UTC (VContainer). Generated: not measured. |
| Unity | `6000.7.0a6` |
| Hardware | AMD Ryzen 9 7900X 12-Core Processor (24 cores), 63383 MB, Linux 7.1 Omarchy 4.0.1 64bit |
| Warmup protocol | 1 discarded full pass (Build of the 3-scope tree + first ResolveAll of all 12 marker phases from the deepest scope + 10k singleton + 10k transient + Dispose). Then `GC.Collect()`, `GC.WaitForPendingFinalizers()`, `GC.Collect()`. Then one measured pass, each metric timed separately with `Stopwatch.GetTimestamp` and `GC.GetAllocatedBytesForCurrentThread` (GC collect is *before* the stopwatch, not inside it). JIT / domain reload is not mixed into the numbers. |

Table cells are raw printer output (`G17` ms, integer ticks). No averages, no rounding. Em-dash means not measured — do not invent.

`GC.GetAllocatedBytesForCurrentThread` is the spec meter and was called. On this Editor it stays **0** even for `new byte[1024]` (probed 2026-09-10); `GC.GetTotalMemory` does move. **Do not read 0 allocated bytes as "zero allocations".**

## Results

Two Explicit Test Runner passes in one Editor session: `OwnContainer_SyntheticGraph_RecordsMetrics` at 05:20:59 UTC, then `VContainer_SyntheticGraph_RecordsMetrics` at 05:21:09 UTC. Generated column waits the emitter (1), entity scopes (1c) and runtime stitch (2).

First ResolveAll item count is **not the same**: VContainer 16 (`ContainerLocal`), Own 26 (`ResolveAll`). Do not treat that row as an identical consume path.

| Metric | VContainer | Runtime plan | Generated | Hardware | Unity | Warmup | Date |
|---|---|---|---|---|---|---|---|
| Build (ms) | 0.21210000000000001 | 0.42449999999999999 | — | AMD Ryzen 9 7900X 12-Core Processor (24 cores), 63383 MB, Linux 7.1 Omarchy 4.0.1 64bit | 6000.7.0a6 | 1 discarded full pass, then GC.Collect + WaitForPendingFinalizers + GC.Collect | 2026-09-10 |
| Build (ticks) | 2121 | 4245 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| Build (allocated bytes) | 0 | 0 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| First ResolveAll of 12 marker phases (ms) | 0.41220000000000001 | 0.1963 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| First ResolveAll of 12 marker phases (ticks) | 4122 | 1963 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| First ResolveAll of 12 marker phases (allocated bytes) | 0 | 0 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Resolve singleton from deepest (ms) | 3.8220999999999998 | 0.91010000000000002 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Resolve singleton from deepest (ticks) | 38221 | 9101 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Resolve singleton from deepest (allocated bytes) | 0 | 0 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Transient (ms) | 20.3384 | 14.604799999999999 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Transient (ticks) | 203384 | 146048 | — | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Transient (allocated bytes) | 0 | 0 | — | same | 6000.7.0a6 | same | 2026-09-10 |

### Hot path (card / player)

Waits step 1c. Not measured. Do not invent numbers.

| Metric | VContainer | Runtime plan | Generated | Notes |
|---|---|---|---|---|
| Card instantiate | — | — | — | waits 1c |
| Player instantiate | — | — | — | waits 1c |

### Raw VContainer passes (unrounded)

Independent harness runs. Not averaged. Pass C is the table above.

| Metric | Pass A 2026-09-10 03:46:27 UTC (first after compile) | Pass B 2026-09-10 03:48:21 UTC | Pass C 2026-09-10 05:21:09 UTC (table above) |
|---|---|---|---|
| Build (ms / ticks / bytes) | 0.28639999999999999 / 2864 / 0 | 0.1903 / 1903 / 0 | 0.21210000000000001 / 2121 / 0 |
| First ResolveAll 12 phases (ms / ticks / bytes) | 0.4834 / 4834 / 0 | 0.38429999999999997 / 3843 / 0 | 0.41220000000000001 / 4122 / 0 |
| 10k singleton from deepest (ms / ticks / bytes) | 4.3426 / 43426 / 0 | 4.0202 / 40202 / 0 | 3.8220999999999998 / 38221 / 0 |
| 10k Transient (ms / ticks / bytes) | 21.430900000000001 / 214309 / 0 | 20.538799999999998 / 205388 / 0 | 20.3384 / 203384 / 0 |

### Raw runtime-plan (Own) pass (unrounded)

One pass, 2026-09-10 05:20:59 UTC, same Editor as VContainer pass C. Previously `ContainerBuilder.Build` threw `NotImplementedException` (03:48:21 UTC).

| Metric | Pass 2026-09-10 05:20:59 UTC |
|---|---|
| Build (ms / ticks / bytes) | 0.42449999999999999 / 4245 / 0 |
| First ResolveAll 12 phases (ms / ticks / bytes) | 0.1963 / 1963 / 0 |
| 10k singleton from deepest (ms / ticks / bytes) | 0.91010000000000002 / 9101 / 0 |
| 10k Transient (ms / ticks / bytes) | 14.604799999999999 / 146048 / 0 |

Stopwatch frequency on this machine: 10_000_000 (100 ns / tick).

## Graph

All three sides apply the same registration table (`BenchmarkGraph.Registrations`) when they can build. Generated cannot yet: the synthetic graph has no installer root for the emitter, and steps 1 / 1c / 2 are not closed.

| | Count | Where |
|---|---|---|
| Services | 32 | 12 root + 12 match + 8 card |
| Scope depth | 3 | Root (Global) → Match (GamePlay) → Card (entity) |
| Singleton | 18 | 12 root + 3 match + 3 card |
| Scoped | 9 | match only |
| Transient | 5 | card only |
| Marker interfaces | 12 | consumed from the deepest scope |

Lifetime mix matches the production explicit-call counts (18 / 9 / 5). Depth matches Global → match → `CardScope`.

### Marker interfaces (EventLoop-shaped)

| Benchmark | Production |
|---|---|
| `IBenchBaseSetup` | `IScopeBaseSetup` |
| `IBenchBaseSetupAsync` | `IScopeBaseSetupAsync` |
| `IBenchSetup` | `IScopeSetup` |
| `IBenchSetupAsync` | `IScopeSetupAsync` |
| `IBenchSetupCompletion` | `IScopeSetupCompletion` |
| `IBenchSetupCompletionAsync` | `IScopeSetupCompletionAsync` |
| `IBenchLoaded` | `IScopeLoaded` |
| `IBenchLoadedAsync` | `IScopeLoadedAsync` |
| `IBenchDispose` | `IScopeDispose` |
| `IBenchDisposeAsync` | `IScopeDisposeAsync` |
| `IBenchSceneService` | `ISceneService` |
| `IBenchEntityComponent` | `IEntityComponent` |

Empty phases are intentional (`IBenchBaseSetupAsync`, `IBenchSetupCompletion`, `IBenchSetupCompletionAsync`, `IBenchLoadedAsync`, `IBenchDisposeAsync`) — production `IScopeSetupCompletion` / `IScopeDispose` are empty too; ResolveAll of an empty list is part of the cost.

Consume path (same as `EventLoop.ResolveList<T>`):

- VContainer: `resolver.Resolve<ContainerLocal<IReadOnlyList<T>>>().Value` on the card scope
- Own: `container.ResolveAll<T>()` on the card scope

### Hot resolves

- Singleton 10k: `BenchRootTime` (root singleton) resolved from the card scope — parent-chain walk on VContainer, flattened slot on own.
- Transient 10k: `BenchCardAction` (card transient; deps are singleton/scoped, already built).

### Injection

Public constructors with the largest arity. No `[Inject]`, no `Construct` methods. Both containers resolve that constructor, so the graph stays identical.

VContainer `Build` is lazy. Own `Build` is eager for Singleton/Scoped (locked 5). That difference is real and belongs in the Build vs first ResolveAll split — do not "fix" it in the harness.

## How to run

Edit-mode, category `Container`. Timed methods are `[Explicit]` so CI / Run All skip them.

1. Unity Test Runner → EditMode → `Internal.Tests` → `ContainerBenchmarkTests`
2. Run Selected:
   - `Graph_MatchesProductionShape` (shape only, not timed)
   - `VContainer_SyntheticGraph_RecordsMetrics`
   - `OwnContainer_SyntheticGraph_RecordsMetrics`
   - `GeneratedContainer_SyntheticGraph_RecordsMetrics` (throws until steps 1, 1c and 2 emit a class for this graph)
3. Or menu **Tools → Container → Run Benchmark** (VContainer always; runtime-plan and generated exceptions are logged, not invented)

Paste the `Debug.Log` / Test Runner output into the table. Keep ticks and allocated bytes as printed.

Harness: `client/Assets/Common/Internal/Tests/Editor/Container/Benchmarks/`.
