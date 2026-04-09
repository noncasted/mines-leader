# Dependency Injection: VContainer & MonoBehaviour Services

**TL;DR: Every MonoBehaviour in VContainer MUST implement ISceneService + IScopeSetup + have Create() method + use Lifetime for subscriptions.**

## The Pattern (MANDATORY)

```csharp
public class MyService : MonoBehaviour, ISceneService, IScopeSetup {
    [Inject] private Character _character;

    public void Create(IScopeBuilder builder) {
        // Register this component
        builder.RegisterComponent(this).As<IScopeSetup>();
    }

    public void OnSetup(IReadOnlyLifetime lifetime) {
        // All initialization here (NOT in Awake/Start)
        _button.ListenClick(lifetime, OnClick);
        _property.View(lifetime, OnPropertyChanged);
    }

    private void OnClick() { }
    private void OnPropertyChanged(int value) { }
}
```

## Full Code Examples

Complete examples with all DI container scenarios:
→ [Assets/Common/Docs/Claude/Docs_Container.cs](../../client/Assets/Common/Docs/Claude/Docs_Container.cs)

Specific examples:
- [ISceneService pattern](../../client/Assets/Common/Docs/Claude/Docs_Container.cs#L112)
- [MonoBehaviour service](../../client/Assets/Common/Docs/Claude/Docs_Container.cs#L129)
- [Scope builder usage](../../client/Assets/Common/Docs/Claude/Docs_Container.cs#L211)
- [IViewInjector pattern](../../client/Assets/Common/Docs/Claude/Docs_Container.cs#L164)
- [Scope lifecycle phases](../../client/Assets/Common/Docs/Claude/Docs_Container.cs#L24)

## Scope Lifecycle

```
1. Create Phase (all Create() methods)
2. Setup Phase (all OnSetup() methods called with scope Lifetime)
3. Gameplay (use subscriptions)
4. Dispose Phase (all Lifetimes terminate, cleanup)
```

## Key Rules

| Rule | Impact | Fix |
|------|--------|-----|
| Missing `ISceneService` | VContainer doesn't know to call Create() | Add interface |
| Missing `IScopeSetup` | OnSetup never called | Add interface |
| Missing `Create()` method | Component not registered | Implement Create() |
| Missing `RegisterComponent()` call | Component invisible to scope | Call in Create() |
| Missing Lifetime parameter | Memory leak | Add lifetime to all subscriptions |
| Init in Awake/Start | Wrong timing | Move to OnSetup() |

## Common Mistakes

❌ **Forgot IScopeSetup**
```csharp
public class Service : MonoBehaviour, ISceneService {
    public void OnSetup(IReadOnlyLifetime lt) { } // Never called!
}
```
✅ **Fix:**
```csharp
public class Service : MonoBehaviour, ISceneService, IScopeSetup {
    public void OnSetup(IReadOnlyLifetime lt) { } // Now called
}
```

---

❌ **Forgot to Register**
```csharp
public void Create(IScopeBuilder builder) {
    // Forgot RegisterComponent!
}
```
✅ **Fix:**
```csharp
public void Create(IScopeBuilder builder) {
    builder.RegisterComponent(this).As<IScopeSetup>();
}
```

---

❌ **Init in Awake (timing is wrong)**
```csharp
public void Awake() {
    _button.ListenClick(???, OnClick); // What lifetime?
}
```
✅ **Fix:**
```csharp
public void OnSetup(IReadOnlyLifetime lifetime) {
    _button.ListenClick(lifetime, OnClick);
}
```

---

❌ **Forgot Lifetime (memory leak)**
```csharp
public void OnSetup(IReadOnlyLifetime lifetime) {
    _property.View(lifetime, OnChange);
    _button.ListenClick(lifetime, OnClick);
    _events.Advise(null, OnEvent); // ❌ LEAK
}
```
✅ **Fix:**
```csharp
public void OnSetup(IReadOnlyLifetime lifetime) {
    _events.Advise(lifetime, OnEvent); // ✅ With lifetime
}
```

## Quick Checklist

→ [rules/MONOBEHAVIOUR.md](../rules/MONOBEHAVIOUR.md)

## VContainer Flow

```csharp
// 1. Create scope with all services
var builder = new ScopeBuilder();
characterService.Create(builder);
uiService.Create(builder);

// 2. Build and initialize
var scope = builder.Build();
// → All Create() called
// → All OnSetup() called with scope Lifetime
// → All subscriptions registered

// 3. Use services
await gameplay.Run(scope.Lifetime);

// 4. Cleanup
scope.Dispose();
// → All Lifetimes terminate
// → All subscriptions cleaned
```

## Registration Variants

```csharp
// Most common
builder.RegisterComponent(this).As<IScopeSetup>();

// As custom interface
builder.RegisterComponent(this).As<IMyService>();

// Multiple interfaces
builder.RegisterComponent(this)
    .As<IScopeSetup>()
    .As<IMyService>();
```

## IViewInjector Pattern

For data-only components that need field injection but no initialization:

```csharp
public class DataComponent : MonoBehaviour, IViewInjector {
    [Inject] public CharacterData Character { get; set; }
    [Inject] public InventoryData Inventory { get; set; }

    public void Inject(IObjectResolver resolver) {
        resolver.Inject(this);  // Auto-inject all [Inject] fields
    }
}

// Usage
var component = GetComponent<DataComponent>();
builder.Inject(component);  // Call Inject() to populate fields
```

**When to use:**
- ✅ Component is pure data holder (no methods, no subscriptions)
- ✅ Needs [Inject] field resolution but no OnSetup initialization
- ❌ Component has event subscriptions (use IScopeSetup instead)
- ❌ Component needs to listen to something (use IScopeSetup instead)

## Advanced Lifecycle Phases

**Standard flow:**

```
Scope Creation:
1. Create() called on all ISceneService components
   → Register with builder
   → No field injection yet

2. Inject [Inject] fields on all components
   → Dependencies now available

3. OnSetup() called on all IScopeSetup components
   → With scope Lifetime parameter
   → Subscribe to events
   → Start listening to properties

4. Gameplay runs
   → All subscriptions active
   → Callbacks fire as events occur

5. Scope Dispose
   → All Lifetimes terminate
   → All subscriptions cleaned automatically
   → Components destroyed
```

**With custom setup ordering:**

```csharp
// In some scope builder code:
builder.RegisterComponent(serviceA).As<IScopeSetup>();
builder.RegisterComponent(serviceB).As<IScopeSetup>();
// → OnSetup() called in registration order (A then B)

// If B depends on A being initialized first:
public class ServiceB : MonoBehaviour, IScopeSetup {
    [Inject] private ServiceA _serviceA;

    public void OnSetup(IReadOnlyLifetime lifetime) {
        // _serviceA.OnSetup() already called (due to registration order)
        _serviceA.OnInitialized.Advise(lifetime, OnServiceAReady);
    }
}
```

**Advanced phases:**

```csharp
// IScopeBaseSetup - called BEFORE OnSetup (for initialization dependencies)
public class Service : MonoBehaviour, ISceneService, IScopeBaseSetup {
    public void OnBaseSetup(IReadOnlyLifetime lifetime) {
        // Called first, before other OnSetup() calls
        Initialize();
    }
}

// IScopeSetupAsync - async initialization
public class AsyncService : MonoBehaviour, ISceneService, IScopeSetupAsync {
    public async UniTask OnSetupAsync(IReadOnlyLifetime lifetime) {
        await LoadResources();
    }
}
```

## Gotchas & Edge Cases

### Create() Called Before Inject

```csharp
public class Service : MonoBehaviour, ISceneService, IScopeSetup {
    [Inject] public Character Character { get; set; }

    public void Create(IScopeBuilder builder) {
        // ⚠️ Character is still null here!
        Debug.Log(Character?.Name);  // null

        builder.RegisterComponent(this).As<IScopeSetup>();
    }

    public void OnSetup(IReadOnlyLifetime lifetime) {
        // Now Character is injected
        Debug.Log(Character.Name);  // Has value
    }
}
```

**Why:** Create() is called during builder setup, before injection happens.

### OnSetup Call Order

```csharp
builder.RegisterComponent(serviceA).As<IScopeSetup>();
builder.RegisterComponent(serviceB).As<IScopeSetup>();

// OnSetup() called in registration order:
// 1. serviceA.OnSetup() first
// 2. serviceB.OnSetup() second
// If B depends on A being initialized, it will work.
// But if A depends on B, it will fail.
```

**Why:** OnSetup() is called immediately after service is registered.

### RegisterComponent vs Inject

```csharp
public class Service : MonoBehaviour, ISceneService, IScopeSetup {
    [Inject] private Character _character;

    public void Create(IScopeBuilder builder) {
        // Option 1: RegisterComponent registers this service
        builder.RegisterComponent(this).As<IScopeSetup>();

        // Option 2: Inject just gets dependencies
        builder.Inject(this);  // [Inject] fields now filled

        // Can do both - Inject for field resolution, RegisterComponent for registration
        builder.Inject(this);
        builder.RegisterComponent(this).As<IScopeSetup>();
    }
}
```

**Why:** Inject fills fields, RegisterComponent adds to container.

### Multiple As<> Registrations

```csharp
public class Service : MonoBehaviour, ISceneService, IScopeSetup, IMyService {
    public void Create(IScopeBuilder builder) {
        builder.RegisterComponent(this)
            .As<IScopeSetup>()
            .As<IMyService>();
        // Now both interfaces point to this instance
    }
}

// Can get by any interface
var setupInterface = scope.Resolve<IScopeSetup>();  // Same instance
var serviceInterface = scope.Resolve<IMyService>();  // Same instance
Debug.Log(ReferenceEquals(setupInterface, serviceInterface));  // true
```

**Why:** All As<> calls register the same object under different interfaces.

### Lifetime Termination Before OnSetup

```csharp
public class Service : MonoBehaviour, ISceneService, IScopeSetup {
    public void Create(IScopeBuilder builder) {
        // Some error during registration
        throw new Exception("Registration failed");
    }

    public void OnSetup(IReadOnlyLifetime lifetime) {
        // ❌ Never called if Create() throws
    }
}

// If Create() throws, the scope build fails and OnSetup never fires
```

**Why:** Exception in Create() prevents service registration and subsequent OnSetup.

### Duplicate Registration

```csharp
public class Service : MonoBehaviour, ISceneService, IScopeSetup {
    public void Create(IScopeBuilder builder) {
        builder.RegisterComponent(this).As<IScopeSetup>();
        builder.RegisterComponent(this).As<IScopeSetup>();  // Duplicate!
    }
}

// ⚠️ OnSetup() called twice - once per registration
```

**Why:** Each registration creates a separate container entry.

## Related
- **Lifetimes:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **Code Style:** [CODE_STYLE.md](../rules/CODE_STYLE.md)
