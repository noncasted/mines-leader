# Own DI vs VContainer — benchmark

Status: two columns, VContainer and the generated scope classes, measured in the same Editor session on the same registration table. The runtime plan column is gone together with the runtime plan (locked 12). Allocations are measured for real since 2026-09-10 10:33 UTC; earlier tables had 0 bytes everywhere and are removed.

| Field | Value |
|---|---|
| Date measured | 2026-09-10 17:44:07 UTC (Generated), 2026-09-10 17:44:10 UTC (VContainer); a second pass at 17:43 is within harness noise |
| Diagnostics | Generated runs with `ContainerRegistryDebug.IsEnabled = false` (`GeneratedContainerBenchmarkHost.WithoutDiagnostics`), the same as a release build. VContainer diagnostics are off too. |
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
| Build | 0.1950 (0.1910 - 0.2260) | 0.0658 (0.0599 - 0.0839) | 32352 | 7524 |
| Build + resolve all 32 services | 0.3500 (0.3454 - 0.3817) | 0.0726 (0.0681 - 0.0880) | 45060 | 7708 |
| First ResolveAll of 12 marker phases | 0.3797 (0.3711 - 0.3960) | 0.0167 (0.0143 - 0.0211) | 31860 | 360 |
| 10k Resolve singleton from deepest | 4.0571 (4.0185 - 4.1300) | 1.5098 (1.5062 - 1.5517) | 520 | 568 |
| 10k Transient | 20.6153 (20.4995 - 20.9487) | 0.9599 (0.9537 - 1.0109) | 400568 | 400568 |

Reading the rows:

- **Build.** VContainer is lazy and creates no instances here; Generated creates all 27 singleton/scoped instances eagerly and still allocates 4.3x less.
- **Build + resolve all.** The fair "everything exists" comparison: 45.1 KB vs 7.7 KB (5.8x), 4.8x faster. Generated resolve after Build adds 184 bytes (transients created by the resolve).
- **10k singleton.** Both are 0 bytes per resolve; the 288–568 bytes are a fixed cost of the loop, the same on both sides. Generated got ~5% slower than with the per-instance dictionary (1.44 → 1.51 ms): a lookup in the static table plus the `GetExport` switch.
- **10k Transient.** Both allocate only the object itself: 40 bytes x 10k (`BenchCardAction`, 3 refs). The difference is time only (21x).

Previous run (2026-09-10 17:24 UTC, per-instance `_exports`, diagnostics on): Generated Build 11056, Build + resolve 11240, singleton 1.4373 ms.

## Where Generated Build bytes go

First column: manual replay of `OpenSession` step by step through the public API (no harness), minimum of 9 runs, byte-exact. Last two columns: `GeneratedContainer_BuildAllocations_ByStep`, minimum over two passes of 11 runs. The harness adds up to 48 bytes on some steps that the manual replay does not have (see above), so small differences between the first column and the others are not real.

| Step | Before: per-instance `_exports`, diagnostics on (bytes) | Now, diagnostics on (bytes) | Now, diagnostics off (bytes) |
|---|---|---|---|
| Session: Lifetime | 48 | 48 | 48 |
| Root: ContainerBuilder | 160 | 120 | 120 |
| Root: RootBuilder + Registry + EventLoop | 160 | 160 | 160 |
| Root: installer (12 registrations) | 1184 | 1040 | 1040 |
| Root: RegisterInstance(Events) | 176 | 160 | 160 |
| Root: ScopeContainer.Create | 2120 | 1420 | 1116 |
| Match: ContainerBuilder | 160 | 120 | 120 |
| Match: RootBuilder + Registry + EventLoop | 160 | 160 | 160 |
| Match: installer (12 registrations) | 1184 | 992 | 1040 |
| Match: RegisterInstance(Events) | 176 | 160 | 160 |
| Match: ScopeContainer.Create | 2268 | 1616 | 1312 |
| Card: ContainerBuilder | 160 | 120 | 120 |
| Card: RootBuilder + Registry + EventLoop | 160 | 160 | 160 |
| Card: installer (8 registrations) | 736 | 608 | 656 |
| Card: RegisterInstance(Events) | 336 | 320 | 368 |
| Card: ScopeContainer.Create | 1300 | 1144 | 744 |
| Session: Dispose | 384 | 432 | 432 |
| **Build (sum without Dispose)** | **10488** | **8348** | **7484** |

What changed:

| Change | Bytes |
|---|---|
| `_exports` is a static `Dictionary<Type, int>` per generated class plus a `GetExport(int)` switch over the fields; the scope no longer allocates its own `Dictionary<Type, object>` (788 / 788 / 340) | ≈ −1500 |
| Diagnostics off: no `ContainerDiagnostics`, no `ContainerRegistryDebug` entry, no lifetime listener closure (304 / 304 / 400 per scope) | ≈ −1000 |
| `ServiceRegistration` 72 → 56: keeps `ContainerBuilder` (the builder and the "built" flag come from it); `Lifetime`, `IsExisting` and `_frozen` removed. −16 x 35 registrations | −560 |
| `ContainerBuilder._injections` is lazy: −40 x 3 scopes | −120 |

In a release build (no `UNITY_EDITOR`, no `DEBUG`) the generated classes do not create diagnostics, and the static `_registrationInfos` / `_buildOrder` tables are not compiled in.

History of the installer cost: ~310 bytes per `Register<T>()` at first (eager `List<Type>` 104 + empty `Dictionary<Type, object>` 80 + `ContainerRegistration` 32 + `ServiceRegistration` 64 + list growth), now ~80: `ServiceRegistration` (56) and growth of `ContainerBuilder._registrations` (320 bytes per scope for 9–16 registrations).

`ScopeContainer.Create` with diagnostics off: the instances (~764 over three scopes), marker arrays, child `Lifetime` + listener delegate and list, `GeneratedScopeRequest`, `GeneratedViewInjector`.

`Session: Dispose` allocates: `Lifetime.Terminate` calls `_parent.RemoveListener(Terminate)`, which creates a new delegate (128 bytes) per scope just to compare it.

## Many entity scopes at once

`GeneratedContainer_ManyScopes_RecordsMetrics`: 500 entity scopes created one after another under one Match scope, in one frame, then disposed. Diagnostics off. Time = median, bytes = min over 11 runs. Measured 2026-09-11 in the Editor (Mono); IL2CPP / WebGL not measured.

| Scope | Build 500 (ms) | Per scope (ms) | Build 500 allocated (bytes) | Per scope (bytes) | Dispose 500 (ms) | Dispose 500 allocated (bytes) |
|---|---|---|---|---|---|---|
| Card (8 registrations: 3 Singleton, 5 Transient) | 3.53 | 0.0071 | 876 000 | 1 752 | 0.38 | 64 568 |
| Card50 (50 registrations: 40 Singleton, 10 Transient) | 8.90 | 0.0178 | 3 204 568 | 6 409 | 0.43 | 64 568 |

`Card50` (`BenchmarkLargeRoots.Card50`, `BenchmarkLargeScope.cs`) is not part of the table graph: the first 10 services take Match/Root services, the rest take neighbours in the scope. Live memory is not in the table: `GC.GetTotalMemory` in Unity Mono moves in heap chunks and gave a negative delta for 500 Card scopes.

Dispose cost does not depend on the registration count: it is the child `Lifetime` (a new `Terminate` delegate for `RemoveListener`, 128 bytes per scope) and the linear search in the parent listener list.

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
   - `GeneratedContainer_ManyScopes_RecordsMetrics`
3. Or menu **Tools → Container → Run Benchmark** (VContainer, then Generated)

Not Explicit, run with the category: `Graph_MatchesProductionShape`, `GeneratedContainer_Roots_MatchTable`, `Measure_CountsManagedAllocations`.

Harness: `client/Assets/Common/Internal/Tests/Editor/Container/Benchmarks/`.
