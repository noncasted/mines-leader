# Own DI vs VContainer — benchmark

Status: two columns, VContainer and the generated scope classes, measured in the same Editor session on the same registration table. The runtime plan column is gone together with the runtime plan (locked 12). Allocations are measured for real since 2026-09-10 10:33 UTC; earlier tables had 0 bytes everywhere and are removed.

| Field | Value |
|---|---|
| Date measured | 2026-09-10 17:24:21 UTC (Generated), 2026-09-10 17:24:24 UTC (VContainer) |
| Unity | `6000.7.0a6`, Mono 6.13.0, incremental GC |
| Hardware | AMD Ryzen 9 7900X 12-Core Processor (24 cores), 63383 MB, Linux 7.1 Omarchy 4.0.1 64bit |
| Warmup | 1 discarded full pass (Build + resolve every service + 12 ResolveAll from the deepest scope + 10k singleton + 10k transient + Dispose). JIT / domain reload is not in the numbers. |
| Runs | 11 measured passes, fresh sessions each. `GC.Collect()`, `GC.WaitForPendingFinalizers()`, `GC.Collect()` before every metric, outside the stopwatch. |
| Time | `Stopwatch.GetTimestamp`, median of 11 runs, min-max next to it. Stopwatch frequency 10_000_000 (100 ns / tick). |
| Allocations | `ProfilerRecorder` on counter `GC Allocated In Frame` (category Memory), delta around the action, **minimum** of 11 runs. |

## How allocations are measured

`GC.GetAllocatedBytesForCurrentThread` is a stub in Unity Mono: it returns 0 always, even for `new byte[1000000]` (probed 2026-09-10). `GC.GetTotalMemory` and `Profiler.GetMonoUsedSizeLong` move in 4 KB heap pages and do not see small objects. `GC.GetTotalAllocatedBytes` does not exist in this profile.

`GC Allocated In Frame` counts every managed allocation byte-exact and works without the Profiler window: `new object()` = 16, `new byte[100]` = 132, `new byte[1000000]` = 1000032, empty action = 0. Two properties shape the protocol:

- It counts **all threads**, so editor background threads can add bytes to a run. Noise only adds, so the minimum over runs is the real cost; the max is printed to show the noise.
- It resets when a profiler frame ends. A test runs synchronously inside one editor frame; `BenchmarkMeasure.Capture` throws if the frame closed during the measurement.

Delegates that the harness itself needs are created before the capture, so their bytes are not in the metric. `Measure_CountsManagedAllocations` (not Explicit) fails if the meter goes back to returning 0 or starts counting its own overhead.

The harness is not free of offsets: between two Editor sessions the same VContainer code moved +328 bytes in Build and in the 10k singleton loop, and `GeneratedContainer_BuildAllocations_ByStep` shows 48-byte extras on some steps that a manual step-by-step replay without the harness does not have. Compare columns of one run; for per-step attribution use the manual replay (table below).

## Results

First ResolveAll item count is **not the same**: VContainer 16 (`ContainerLocal<IReadOnlyList<T>>`), Generated 7 (`ResolveAll<T>`, local to the card scope). Do not compare that row as an identical consume path.

| Metric | VContainer time (ms) | Generated time (ms) | VContainer allocated (bytes) | Generated allocated (bytes) |
|---|---|---|---|---|
| Build | 0.2041 (0.1933 - 0.2211) | 0.0874 (0.0816 - 0.0927) | 32352 | 11056 |
| Build + resolve all 32 services | 0.3670 (0.3535 - 0.3699) | 0.0932 (0.0857 - 0.0964) | 45012 | 11240 |
| First ResolveAll of 12 marker phases | 0.3913 (0.3771 - 0.4140) | 0.0194 (0.0161 - 0.0241) | 29892 | 360 |
| 10k Resolve singleton from deepest | 4.0081 (3.9792 - 7.3427) | 1.4373 (1.4169 - 1.4572) | 520 | 520 |
| 10k Transient | 20.5934 (20.4529 - 20.9859) | 0.9780 (0.9590 - 1.0267) | 400568 | 400424 |

Reading the rows:

- **Build.** VContainer is lazy and creates no instances here; Generated creates all 27 singleton/scoped instances eagerly and still allocates 2.9x less.
- **Build + resolve all.** The fair "everything exists" comparison: 45.0 KB vs 11.2 KB (4.0x), 3.9x faster. Generated resolve after Build adds 184 bytes (transients created by the resolve).
- **10k singleton.** Both are 0 bytes per resolve; ~520 bytes is a fixed cost of the loop, the same on both sides.
- **10k Transient.** Both allocate only the object itself: 40 bytes x 10k (`BenchCardAction`, 3 refs). The difference is time only (21x).

## Where Generated Build bytes go

Manual replay of `OpenSession` step by step through the public API (no harness), minimum of 9 runs per step; byte-exact, matches the sum of object sizes.

| Step | Lazy registration collections (bytes) | Without `ContainerRegistration` (bytes) |
|---|---|---|
| Session: Lifetime | 48 | 48 |
| Root: ContainerBuilder | 152 | 160 |
| Root: RootBuilder + ServiceCollection / Registry + EventLoop | 192 | 160 |
| Root: installer (12 registrations) | 1472 | 1184 |
| Root: RegisterInstance(Events) | 200 | 176 |
| Root: ScopeContainer.Create | 2065 | 2120 |
| Match: ContainerBuilder | 152 | 160 |
| Match: RootBuilder + ServiceCollection / Registry + EventLoop | 192 | 160 |
| Match: installer (12 registrations) | 1472 | 1184 |
| Match: RegisterInstance(Events) | 200 | 176 |
| Match: ScopeContainer.Create | 2109 | 2268 |
| Card: ContainerBuilder | 152 | 160 |
| Card: RootBuilder + ServiceCollection / Registry + EventLoop | 192 | 160 |
| Card: installer (8 registrations) | 928 | 736 |
| Card: RegisterInstance(Events) | 360 | 336 |
| Card: ScopeContainer.Create | 1141 | 1300 |
| Session: Dispose | 384 | 384 |
| **Build (sum without Dispose)** | **11027** | **10488** |

Changes between the two columns (−539 in total):

| Change | Bytes |
|---|---|
| `ContainerRegistration` wrapper removed, `ServiceRegistration` keeps `IBuilder` (64 → 72): −24 x 35 registrations | −840 |
| `ContainerBuilder.Builder` field: +8 x 3 scopes | +24 |
| `ServiceCollection` removed: −32 x 3 scopes | −96 |
| `ContainerDiagnostics` in `ScopeContainer.Create` (loaded assets, children) | +373 |

History of the installer cost: ~310 bytes per `Register<T>()` at first (eager `List<Type>` 104 + empty `Dictionary<Type, object>` 80 + `ContainerRegistration` 32 + `ServiceRegistration` 64 + list growth), now ~96: `ServiceRegistration` (72) and growth of `ContainerBuilder._registrations` (320 bytes per scope for 9–16 registrations).

`ScopeContainer.Create`: the instances, the `_exports` dictionary (Dictionary<Type, object> with 20 entries = 788 bytes), marker arrays, child `Lifetime` + listener delegate and list, `GeneratedScopeRequest`, `GeneratedViewInjector`, diagnostics attached to `ContainerRegistryDebug` with a lifetime listener closure.

`Session: Dispose` allocates: `Lifetime.Terminate` calls `_parent.RemoveListener(Terminate)`, which creates a new delegate (128 bytes) per scope just to compare it.

### Hot path (card / player)

Not measured. Do not invent numbers.

| Metric | VContainer | Generated | Notes |
|---|---|---|---|
| Card instantiate | — | — | not measured |
| Player instantiate | — | — | not measured |

## Graph

Both sides apply the same registration table (`BenchmarkGraph.Registrations`). Generated gets it through `BenchmarkRoots` (static installer calls the generator can see); `GeneratedContainer_Roots_MatchTable` checks every registration and marker count against the table.

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
- Generated: `container.ResolveAll<T>()` on the card scope

### Hot resolves

- Singleton 10k: `BenchRootTime` (root singleton) resolved from the card scope — parent-chain walk on both.
- Transient 10k: `BenchCardAction` (card transient; deps are singleton/scoped, already built).

### Injection

Public constructors with the largest arity. No `[Inject]`, no `Construct` methods. Both containers resolve that constructor, so the graph stays identical.

VContainer `Build` is lazy. Generated `Build` is eager for Singleton/Scoped (locked 5). That difference is real — compare "Build + resolve all" for the state where every instance exists; do not "fix" it in the harness.

## How to run

Edit-mode, category `Container`. Timed methods are `[Explicit]` so CI / Run All skip them.

1. Unity Test Runner → EditMode → `Internal.Tests` → `ContainerBenchmarkTests`
2. Run Selected:
   - `VContainer_SyntheticGraph_RecordsMetrics`
   - `GeneratedContainer_SyntheticGraph_RecordsMetrics`
   - `GeneratedContainer_BuildAllocations_ByStep`
3. Or menu **Tools → Container → Run Benchmark** (VContainer, then Generated)

Not Explicit, run with the category: `Graph_MatchesProductionShape`, `GeneratedContainer_Roots_MatchTable`, `Measure_CountsManagedAllocations`.

Harness: `client/Assets/Common/Internal/Tests/Editor/Container/Benchmarks/`.
