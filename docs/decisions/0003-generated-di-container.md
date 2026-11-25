# 0003. Generated DI container instead of VContainer / Zenject

Status: accepted (2026-09). VContainer left the game code on 2026-09-10 and the repository on 2026-09-21.

## Problem

The client is a WebGL build: IL2CPP, managed code stripping and a size budget (see [0005](0005-webgl-build-size.md)).
Reflection-based containers need `link.xml` / `[Preserve]` for every injected type, build their graph at runtime and
report a missing registration only when the scope is opened in a running game. The game has a deep scope chain
(Internal, Global, Meta, then GameLoop, Menu or GamePlay, then an entity scope per card and per player), created again
for every match and every card.

## Decision

- Installers are plain C# calls on `ContainerBuilder` (`Register<T>().As<I>()`, `WithParameter`, ...). A Roslyn
  generator in the framework (`tools/framework/client/core/Source~/ContainerGenerator`) walks them at compile time and
  emits a sealed container class per scope root: constructor calls, slots, a static type-to-slot table.
- There is no runtime reflection and no fallback: a root without a generated class throws, a missing dependency is a
  compile error.
- The graph is checked by the compiler: 18 diagnostics `CINGR001`-`CINGR018` (missing registration, cycle, an entity
  variant that cannot be enumerated, an `[Inject]` method that cannot be called, ...), listed in
  `tools/framework/.claude/docs/COMMON_CONTAINER.md`. A headless `dotnet build client.sln` runs the generator, so a
  broken registration fails the build without the editor.
- Singletons and scoped services are created eagerly at `Build`.

## Numbers

Benchmark: `tools/framework/client/core/Tests/Editor/Container/Benchmarks`, one synthetic graph shaped like the game
(32 services, 3 levels Root -> Match -> Card, 18 singleton / 9 scoped / 5 transient, 12 marker interfaces like the scope
phases). 1 discarded warmup pass, 11 measured passes on fresh sessions, `GC.Collect` before every metric. Time is the
median, bytes the minimum over the runs. AMD Ryzen 9 7900X, Linux.

**Current, generated only** (2026-11-25, Unity 7000.0.0a10 Editor, CoreCLR, diagnostics off as in a release build;
the allocation counter is checked by `Measure_CountsManagedAllocations` against `new byte[1000]`):

| Metric | Time (ms) | Allocated (bytes) |
|---|---:|---:|
| Build of 3 scopes | 0.027 | 3,040 |
| Build + resolve all 32 services | 0.033 | 3,224 |
| First ResolveAll of 12 marker phases | 0.010 | 296 |
| 10k resolves of a root singleton from the deepest scope | 0.257 | 0 |
| 10k transients | 0.513 | 400,000 (the objects themselves) |
| 500 card scopes (8 registrations) under one match | 0.52 | 600 per scope |
| 500 card scopes (50 registrations) under one match | 2.73 | 2,080 per scope |

Disposing 500 card scopes takes 0.05-0.06 ms and allocates nothing.

**Against VContainer** (2026-09-10, Unity 6000.7.0a6 Editor, Mono, same harness and graph, both with diagnostics off;
recorded in git at `b399faa8:docs/tasks/current/own_di/benchmark.md`):

| Metric | VContainer (ms) | Generated (ms) | VContainer (bytes) | Generated (bytes) |
|---|---:|---:|---:|---:|
| Build of 3 scopes | 0.195 | 0.066 | 32,352 | 7,524 |
| Build + resolve all 32 services | 0.350 | 0.073 | 45,060 | 7,708 |
| First ResolveAll of 12 marker phases | 0.380 | 0.017 | 31,860 | 360 |
| 10k singleton resolves | 4.06 | 1.51 | 520 | 568 |
| 10k transients | 20.62 | 0.96 | 400,568 | 400,568 |

VContainer builds lazily and the generated container eagerly, so "Build + resolve all" is the fair row: 4.8x faster
and 5.8x fewer bytes. The two tables are not comparable with each other (Mono then, CoreCLR now; the generated
container also got cheaper since).

Not measured:
- **VContainer on Unity 7000.** It is no longer in the repository, and it was not added back for this record. How:
  restore `VContainerBenchmarkHost` and the VContainer plugin (`client/Assets/Plugins/VContainer`) from commit
  `b399faa8`, reference it from the benchmark test assembly and run both hosts in one session.
- **IL2CPP / WebGL.** All numbers are Editor JIT. How: a development WebGL build with the benchmark behind a menu or a
  `GameMock` mode, run on the `device-lab` stand.

## Cost

- About 9,600 lines of generator code (`Source~/ContainerGenerator`) plus the container runtime, owned and maintained
  by the team.
- Onboarding: a newcomer learns the builder API, the scope phases and the `CINGR` codes instead of a known library.
- The generator sees only what it can walk statically: installers must be plain calls (`CINGR001` / `CINGR002`
  otherwise), variants of an entity are separate roots, and a dependency on a scene service needs the asset map
  regenerated first.
- A generator bug is a compile error in every game on the framework ([0002](0002-shared-framework.md)); its own test
  suite (`ContainerGenerator.Verify`) runs in `tools/scripts/run-tests.sh`.
