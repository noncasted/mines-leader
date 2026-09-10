# Own DI vs VContainer — benchmark

Status: harness run in Editor. **Own container has no numbers** (`ContainerBuilder.Build` still throws). No winner is claimed.

| Field | Value |
|---|---|
| Date measured | 2026-09-10 |
| Unity | `6000.7.0a6` |
| Hardware | AMD Ryzen 9 7900X 12-Core Processor (24 cores), 63383 MB, Linux 7.1 Omarchy 4.0.1 64bit |
| Warmup protocol | 1 discarded full pass (Build of the 3-scope tree + first ResolveAll of all 12 marker phases from the deepest scope + 10k singleton + 10k transient + Dispose). Then `GC.Collect()`, `GC.WaitForPendingFinalizers()`, `GC.Collect()`. Then one measured pass, each metric timed separately with `Stopwatch.GetTimestamp` and `GC.GetAllocatedBytesForCurrentThread` (GC collect is *before* the stopwatch, not inside it). JIT / domain reload is not mixed into the numbers. |

Table cells are raw printer output (`G17` ms, integer ticks). No averages, no rounding.

`GC.GetAllocatedBytesForCurrentThread` is the spec meter and was called. On this Editor it stays **0** even for `new byte[1024]` (probed 2026-09-10); `GC.GetTotalMemory` does move. **Do not read 0 allocated bytes as "zero allocations".**

## Results

Official pass: Unity Test Runner, EditMode, `VContainer_SyntheticGraph_RecordsMetrics`, 2026-09-10 03:48:21 UTC. Own column is the exception from the same Editor, not a time.

| Metric | VContainer | Own | Hardware | Unity | Warmup | Date |
|---|---|---|---|---|---|---|
| Build (ms) | 0.1903 | threw: System.NotImplementedException: The method or operation is not implemented. | AMD Ryzen 9 7900X 12-Core Processor (24 cores), 63383 MB, Linux 7.1 Omarchy 4.0.1 64bit | 6000.7.0a6 | 1 discarded full pass, then GC.Collect + WaitForPendingFinalizers + GC.Collect | 2026-09-10 |
| Build (ticks) | 1903 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| Build (allocated bytes) | 0 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| First ResolveAll of 12 marker phases (ms) | 0.38429999999999997 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| First ResolveAll of 12 marker phases (ticks) | 3843 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| First ResolveAll of 12 marker phases (allocated bytes) | 0 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Resolve singleton from deepest (ms) | 4.0202 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Resolve singleton from deepest (ticks) | 40202 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Resolve singleton from deepest (allocated bytes) | 0 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Transient (ms) | 20.538799999999998 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Transient (ticks) | 205388 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |
| 10k Transient (allocated bytes) | 0 | threw (same) | same | 6000.7.0a6 | same | 2026-09-10 |

First ResolveAll item count on this graph (deepest scope, VContainer `ContainerLocal`): 16.

### Raw VContainer passes (unrounded)

Two independent harness runs after compile. Not averaged.

| Metric | Pass A 2026-09-10 03:46:27 UTC (first after compile) | Pass B 2026-09-10 03:48:21 UTC (Test Runner, table above) |
|---|---|---|
| Build (ms / ticks / bytes) | 0.28639999999999999 / 2864 / 0 | 0.1903 / 1903 / 0 |
| First ResolveAll 12 phases (ms / ticks / bytes) | 0.4834 / 4834 / 0 | 0.38429999999999997 / 3843 / 0 |
| 10k singleton from deepest (ms / ticks / bytes) | 4.3426 / 43426 / 0 | 4.0202 / 40202 / 0 |
| 10k Transient (ms / ticks / bytes) | 21.430900000000001 / 214309 / 0 | 20.538799999999998 / 205388 / 0 |

Stopwatch frequency on this machine: 10_000_000 (100 ns / tick).

## Graph

Both sides apply the same registration table (`BenchmarkGraph.Registrations`).

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
   - `OwnContainer_SyntheticGraph_RecordsMetrics` (throws until track A implements `ContainerBuilder`)
3. Or menu **Tools → Container → Run Benchmark** (VContainer always; own exception is logged, not invented)

Paste the `Debug.Log` / Test Runner output into the table. Keep ticks and allocated bytes as printed.

Harness: `client/Assets/Common/Internal/Tests/Editor/Container/Benchmarks/`.
