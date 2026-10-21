# ContainerGenerator

Roslyn incremental source generator (`ContainerGraphGenerator`) for the Internal container. It reads installer
methods at compile time and emits one concrete container class per scope root. There is no runtime reflection
fallback: a root without a generated class throws `No generated container for '<root>'` when the scope is built.

`Tools~` is ignored by Unity. The built dll lives in `client/Assets/Plugins/ContainerGenerator/` and must stay
labeled `RoslynAnalyzer` with every platform disabled (see the `.meta`). Unity runs it for every assembly it compiles;
`dotnet build client/<Asm>.csproj` runs it too.

## Build / copy

From the repo root:

```bash
dotnet build client/Tools~/ContainerGenerator/ContainerGenerator.csproj -c Release
```

The `CopyToUnityPlugins` post-build target copies `ContainerGenerator.dll` into
`client/Assets/Plugins/ContainerGenerator/`; the shared Unity editor picks it up on the next script compile, so
build in a repo copy unless you mean to replace the plugin. Do not copy `Microsoft.CodeAnalysis*.dll` next to it.

Pin is `Microsoft.CodeAnalysis.CSharp` **4.3.0** (Unity-compatible 4.x), target `netstandard2.0`.

## Verify harness

```bash
dotnet run --project client/Tools~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj -c Release
```

`ContainerGenerator.Verify` compiles stub `Internal` sources plus test installers in memory, runs the walker,
resolver, emitters and the full generator driver, and prints `PASS`/`FAIL` per case and `ALL PASSED`
(exit code 0) or `FAILED N` (exit code 1). Run it after every generator change. It reaches the generator internals
through `InternalsVisibleTo`.

## Pipeline

1. **Roots.** A root is a method marked `[ContainerGraphRoot]`, or a `Construct` / `Build` method with an
   `IScopeBuilder` / `IEntityBuilder` / `IBuilder` parameter. `[ContainerScopeParent(typeof(T), nameof(T.Method))]`
   names the parent scope (can sit on the root or on its containing method).
2. **Walk** (`Graph/GraphWalker`). Follows installer calls from each root into one graph: registrations, `.As<>()`,
   `WithParameter`, markers, alternative `if/else` pairs, entity variants. Builder primitives are recognized by
   `[Internal.ContainerPrimitive(kind)]` on the method (`Graph/PrimitiveKind.cs` mirrors the runtime enum by value);
   an unbound call (code that does not compile yet) falls back to the method name. An `if` around a call into another
   installer is not carried inside that installer (both branches are built).
3. **Manifests** (`Graph/GraphEmitter`, `Scope/ManifestReader`, `Scope/ManifestHarvest`). Every assembly emits
   `ContainerGraph.g.cs` with `[assembly: Internal.ContainerInstaller(...)]` per installer method; consumer
   assemblies read them to resolve installers and parent scopes from other assemblies.
4. **Entity assets** (`Entity/`). Prefab views and their bindings come from
   `[assembly: Internal.ContainerGraphAsset(...)]` in
   `Assets/Common/Internal/Runtime/Tools/Container/Generated/ContainerGraph.AssetMap.cs`, written by the editor
   `ContainerGraphPostprocessor` (also scene services of `SceneServicesFactory`).
5. **Resolve** (`Scope/EdgeResolver`, `Scope/ScopePlan`). Binds every constructor / `[Inject]` parameter to a
   registration, a parent export (typed hole), a collection, or a builder parameter; reports CINGR diagnostics.
6. **Emit** (`Scope/ScopeEmitter`). One `{Type}{Method}Container.g.cs` per root (and per entity variant):
   `internal sealed class {Type}{Method}Container : Internal.IContainer, Internal.IProvides<T>...` with a field per
   registration, ctor-time construction in dependency order, a static export table for `Resolve` / `TryResolve`
   (falls back to the parent), `Dispose` terminating the scope lifetime, and
   `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] RegisterGenerated()` calling
   `Internal.GeneratedScopes.Register(rootId, request => new ...Container(...))`
   (or `RegisterVariant(rootId, typeof(View), ...)` for entity variants).
   A `builder.Inject(x)` slot also takes `request.GetInjections<T>()` (every instance passed with that type, e.g.
   identical scene services) and calls `[Inject]` on each of them in a loop; the slot field keeps the last instance.
   Several `builder.Inject` call sites of one type share a single loop.

At runtime `ScopeContainer` / `EntityScopeLoader` take the root id from the method group
(`GeneratedScopes.RootId`), and `GeneratedScopes.Create` invokes the registered factory with a
`GeneratedScopeRequest` (lifetime, parent, builder parameters, `IsRegistered` for alternatives).

`Injector/` holds the shared symbol lookups (`ReferenceSymbols`) and the per-type constructor / `[Inject]`
analysis (`TypeAnalyzer`, `InjectorModel`) used by the resolver.

## Diagnostics

All are errors.

| Id | Meaning |
|----|---------|
| CINGR001 | Walker does not understand a syntax form in an installer (also wraps generator exceptions) |
| CINGR002 | Installer call cannot be resolved (e.g. a generic helper from another assembly) |
| CINGR003 | Constructor / `[Inject]` parameter has no registration in the scope or its parents; no class is emitted |
| CINGR004 | Circular dependency, with the path |
| CINGR005 | Entity variant condition cannot be enumerated (only `bool` and closed enums may change the graph) |
| CINGR006 | One `ScopeEntityView` type used by two assets |
| CINGR007 | Declared parent scope manifest not found |
| CINGR008 | `builder.Injectable<T>` type has no visible `[Inject]` method |

Check generated code without Unity:
`dotnet build client/<Asm>.csproj --no-incremental --no-dependencies -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<dir>`.
