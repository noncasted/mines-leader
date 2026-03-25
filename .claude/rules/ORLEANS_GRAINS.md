# Orleans Grains (CRITICAL)

Full details: → [docs/COMMON_ORLEANS.md](../docs/COMMON_ORLEANS.md)

## The Pattern

```csharp
// Interface
public interface IMyGrain : IGrainWithGuidKey {
    [Transaction]   // only if called inside a transaction
    Task DoSomething(string value);
}

// Implementation
public class MyGrain : Grain, IMyGrain {
    public MyGrain(
        [State] State<MyState> state,
        IOrleans orleans) {
        _state = state;
        _orleans = orleans;
    }

    private readonly State<MyState> _state;
    private readonly IOrleans _orleans;

    public async Task DoSomething(string value) {
        await _state.Update(s => { s.Value = value; });
    }
}

// State class
[GenerateSerializer]
public class MyState : IStateValue {
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Value { get; set; } = string.Empty;
    public int Version => 0;
}
```

## CRITICAL CHECKLIST

❌ **WILL FAIL IF MISSING:**
- [ ] Interface extends `IGrainWithGuidKey` or `IGrainWithStringKey`
- [ ] Constructor injection — NOT field `[Inject]` (this is Orleans, not VContainer)
- [ ] State injected via `[State]` attribute: `[State] State<T>`
- [ ] `[GenerateSerializer]` on every state class
- [ ] `[Id(N)]` on every property of state class (sequential: 0, 1, 2...)
- [ ] State class implements `IStateValue` with `int Version => 0;`
- [ ] New state entry in `StatesLookup.cs` + registered in `ProjectsSetupExtensions.AddStates()`

❌ **WRONG — use only when method is called inside a transaction:**
- `[Transaction]` — only on methods invoked within transaction scope (custom attribute from `Infrastructure`, NOT Orleans native)
