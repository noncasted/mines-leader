# Orleans Grains (CRITICAL)

Full details: → [docs/COMMON_ORLEANS.md](../docs/COMMON_ORLEANS.md)

## The Pattern

```csharp
// Interface
public interface IMyGrain : IGrainWithGuidKey {
    [Transaction(TransactionOption.Join)]   // only if called inside a transaction
    Task DoSomething(string value);
}

// Implementation
[Reentrant]
public class MyGrain : Grain, IMyGrain {
    public MyGrain(
        [States.MyState] ITransactionalState<MyState> state,
        IOrleans orleans) {
        _state = state;
        _orleans = orleans;
    }

    private readonly ITransactionalState<MyState> _state;
    private readonly IOrleans _orleans;

    public async Task DoSomething(string value) {
        await _state.Update(s => { s.Value = value; });
    }
}

// State class
[GenerateSerializer]
[Alias(States.MyState_Entity)]
public class MyState {
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Value { get; set; } = string.Empty;
}
```

## CRITICAL CHECKLIST

❌ **WILL FAIL IF MISSING:**
- [ ] Interface extends `IGrainWithGuidKey` or `IGrainWithStringKey`
- [ ] `[Reentrant]` on grain class
- [ ] Constructor injection — NOT field `[Inject]` (this is Orleans, not VContainer)
- [ ] State injected via attribute: `[States.X] ITransactionalState<T>`
- [ ] `[GenerateSerializer]` on every state class
- [ ] `[Id(N)]` on every property of state class (sequential: 0, 1, 2...)
- [ ] `[Alias(States.X)]` on state class
- [ ] New state registered in `States.cs` → `StateAttributesExtensions` → `StateTables`

❌ **WRONG — use only when method is called inside a transaction:**
- `[Transaction(TransactionOption.Join)]` — only on methods invoked within transaction scope
