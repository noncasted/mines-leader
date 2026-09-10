# ContainerGenerator

Roslyn incremental source generator for the Internal container.

- **Step 4** emits `{Type}GeneratedInjector : Internal.IInjector` in the same assembly as the type, plus `ContainerInjectors_{Assembly}.Register()`.
- **Step 4b** walks installer methods into a graph description (`Internal.ContainerGraph`). It does **not** emit a generated-per-scope class.

`Tools~` is ignored by Unity. The built dll is copied to `client/Assets/Plugins/ContainerGenerator/` and must stay labeled `RoslynAnalyzer` with every platform disabled (see the `.meta`).

## Build / copy

From the repo root:

```bash
dotnet build client/Tools~/ContainerGenerator/ContainerGenerator.csproj -c Release
```

The post-build target copies `ContainerGenerator.dll` into `client/Assets/Plugins/ContainerGenerator/`. Unity picks it up on the next script compile. Do not copy `Microsoft.CodeAnalysis*.dll` next to it.

Pin is `Microsoft.CodeAnalysis.CSharp` **4.3.0** (Unity-compatible 4.x).

## What it emits

For `DelayRunner(IUpdater updater)`:

```csharp
internal sealed class DelayRunnerGeneratedInjector : global::Internal.IInjector {
    public DelayRunnerGeneratedInjector(int[] slots) {
        _slot0 = slots[0];
    }

    private readonly int _slot0;

    public object Create(global::Internal.IResolvePlan plan) {
        var instance = new global::Internal.DelayRunner(plan.Get<global::Internal.IUpdater>(_slot0));
        Construct(instance, plan);
        return instance;
    }

    public void Construct(object instance, global::Internal.IResolvePlan plan) {
    }
}
```

Per assembly, `ContainerInjectors_{SafeAssemblyName}.Register()` is called from `[RuntimeInitializeOnLoadMethod]` and does:

```csharp
global::Internal.ContainerInjectors.Register(typeof(Foo), slots => new FooGeneratedInjector(slots));
global::Internal.ContainerInjectors.MarkAssemblyCovered(typeof(ContainerInjectors_X).Assembly);
```

`ContainerInjectors` itself is owned by track A (Runtime). This generator only emits the calls.

Disabling or removing the dll must not break a player build: the runtime falls back to reflection until an assembly is marked covered.
