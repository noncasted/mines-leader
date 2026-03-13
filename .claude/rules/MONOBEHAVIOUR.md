# MonoBehaviour Services (CRITICAL)

## The Pattern (DO NOT FORGET)

```csharp
public class MyService : MonoBehaviour, ISceneService, IScopeSetup {
    [Inject] private IDependency _dep;

    public void Create(IScopeBuilder builder) {
        builder.RegisterComponent(this).As<IScopeSetup>();
    }

    public void OnSetup(IReadOnlyLifetime lifetime) {
        _button.ListenClick(lifetime, OnClick);
        _property.View(lifetime, OnChange);
    }
}
```

## CRITICAL CHECKLIST

❌ **WILL FAIL IF MISSING:**
- [ ] `ISceneService` interface
- [ ] `IScopeSetup` interface
- [ ] `Create(IScopeBuilder builder)` method
- [ ] `builder.RegisterComponent(this).As<IScopeSetup>()` call
- [ ] `OnSetup(IReadOnlyLifetime lifetime)` method

❌ **MEMORY LEAK IF MISSING:**
- [ ] ALL subscriptions use `lifetime` parameter
- [ ] ALL buttons: `ListenClick(lifetime, ...)`
- [ ] ALL properties: `View(lifetime, ...)`

## See Full Details
→ [/docs/COMMON_CONTAINER.md](../docs/COMMON_CONTAINER.md)
