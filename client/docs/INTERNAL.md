# Client Internal Module

Critical foundation layer providing dependency injection, lifetime management, reactive system, and application bootstrap infrastructure.

## Module Structure

```
Internal/
├── Common/           - Foundational utilities
│   ├── DataTypes/   - Collections and structures
│   └── Reactive/    - Lifetime and event system
├── Setup/           - Bootstrap configuration
├── Scopes/          - Hierarchical DI management
│   ├── Common/      - Base builders and events
│   ├── Services/    - Service scope loading
│   └── Entities/    - Entity scope building
├── Services/        - Environmental services
│   ├── Environments/ - Asset storage
│   ├── Options/     - Configuration registry
│   └── Scenes/      - Scene loading
└── Editor/          - Editor-only utilities
```

---

## Core Concepts

### 1. Lifetime Management

**IReadOnlyLifetime / ILifetime**

```csharp
public interface IReadOnlyLifetime
{
    CancellationToken Token { get; }
    bool IsTerminated { get; }
    void Listen(Action callback);
    void RemoveListener(Action callback);
}

public interface ILifetime : IReadOnlyLifetime
{
    void Terminate();
}
```

**Features:**

- Hierarchical lifetimes (parent/child relationships)
- Automatic cleanup when parent terminates
- CancellationToken support for async operations
- Safe listener management (ModifiableList for iteration)
- Automatic listener removal on termination

**Usage:**

```csharp
var lifetime = new Lifetime();
lifetime.Listen(() => Console.WriteLine("Cleaned up!"));

// ... do work ...

lifetime.Terminate();  // Invokes all listeners
```

**Hierarchy:**

```
Application Lifetime
  ├── Internal Scope Lifetime
  ├── Global Scope Lifetime (child of Internal)
  ├── Meta Scope Lifetime (child of Global)
  └── GameLoop/Menu/GamePlay Scope Lifetimes (children)
```

### 2. Reactive Event System

**IEventSource<T>**

```csharp
public interface IEventSource<T>
{
    void Advise(IReadOnlyLifetime lifetime, Action<T> handler);
}

// Variants: EventSource, EventSource<T>, EventSource<T1, T2>, EventSource<T1, T2, T3>
```

**Key Pattern: Automatic Cleanup**

```csharp
// Listener removed when lifetime terminates
_eventSource.Advise(lifetime, value => HandleValue(value));
// Automatic: lifetime.Terminate() → listener removed
```

### 3. Observable Properties

**IViewableProperty<T> / LifetimedValue<T>**

```csharp
public interface IViewableProperty<T> : ILifetimedValue<T>
{
    T Value { get; }
    void Set(T value);
}

public class LifetimedValue<T>
{
    public T Value { get; }
    public IReadOnlyLifetime ValueLifetime { get; }

    public void Set(T value)
    {
        // Only notify if value changed
        _value = value;
        _emit(new ValueLifetime(), value);
    }
}
```

**Value-Specific Lifetimes:**

Each emitted value creates NEW lifetime:

```
Set(value1)
  └─ Creates Lifetime1 for all subscribers
  └─ Lifecycle: value1 is current

Set(value2)
  └─ Terminates Lifetime1
  └─ Creates Lifetime2 for all subscribers
  └─ Lifecycle: value2 is current
```

**Benefits:**

- Fine-grained resource management
- Automatic cleanup of old value resources
- Context-aware subscription handling

### 4. Reactive Collections

**ViewableList<T>, ViewableDictionary<K, V>, ModifiableList<T>**

```csharp
public class ViewableList<T> : IViewableCollection<T>
{
    public void Add(T item);
    public void Remove(T item);
    public void Clear();

    public void Advise(IReadOnlyLifetime lifetime, Action<T> onAdded);
}

public class ModifiableList<T>
{
    // Safe iteration: defers Add/Remove during enumeration
    public IEnumerator<T> GetEnumerator()
    {
        // Applies pending changes after iteration
    }
}
```

---

## Dependency Injection System

### Core Builder Interfaces

**IBuilder**

```csharp
public interface IBuilder
{
    IServiceCollection Services { get; }
    IAssetEnvironment Assets { get; }
    IScopeEventListeners Events { get; }
    IReadOnlyLifetime Lifetime { get; }
}
```

**IScopeBuilder : IBuilder**

```csharp
public interface IScopeBuilder : IBuilder
{
    ISceneLoader SceneLoader { get; }
    IServiceScopeBinder Binder { get; }
    ILifetime ScopeLifetime { get; }
    bool IsMock { get; }
}
```

**IEntityBuilder : IBuilder**

```csharp
public interface IEntityBuilder : IBuilder
{
    LifetimeScope Scope { get; }
    ILifetime ScopeLifetime { get; }
    IScopeEntityView View { get; }
}
```

### Service Registration

**ServiceCollection**

```csharp
public class ServiceCollection
{
    private List<RegistrationBuilder> _builders;
    private List<RegistrationBuilder> _selfResolvable;
    private List<InstanceInjection> _injections;

    public void PassRegistrations(IContainerBuilder builder)
    {
        // Registers all to VContainer
    }

    public void Resolve(IObjectResolver resolver)
    {
        // Resolves self-resolvable and injects
    }
}
```

**Builder Extensions**

```csharp
// Fluent registration API
builder.Register<MyService>()
    .As<IMyService>()
    .AsSelf()
    .WithParameter<IDependency>(dependency)
    .As<IAnotherInterface>();

builder.Register<MyComponent>()
    .RegisterComponent<MyComponent>();
```

---

## Bootstrap Process

### Stage 1: Internal Scope Loading

**File:** `Setup/InternalScopeLoader.cs`

```
1. Create container GameObject (Instantiate + DontDestroyOnLoad)
2. Enqueue registration callback with VContainer.LifetimeScope
3. Build VContainer container
   ├─ Setup platform options from config
   ├─ Cache asset environment
   ├─ Create AssetEnvironment
   ├─ Execute preprocessors
   └─ Register scene loaders
4. Attach ILoadedScope to container
```

**Registration Phase:**

```csharp
void Register(IContainerBuilder containerBuilder)
{
    var optionsRegistry = _config.AssetsStorage.Options[_config.Platform];
    optionsRegistry.CacheRegistry();
    optionsRegistry.AddOptions(new PlatformOptions(...));

    _config.AssetsStorage.Cache();
    var assets = new AssetEnvironment(assetsStorage, optionsRegistry);

    var scopeBuilder = new InternalScopeBuilder(assets, containerBuilder);

    foreach (var preprocessor in assets.GetAssets<EnvPreprocessor>())
        preprocessor.Execute();

    scopeBuilder.AddScenes().AddScopeLoaders();
}
```

### Stage 2: Service Scope Loading

**File:** `Scopes/Services/ServiceScopeLoader.cs`

**Flow:**

```
1. Load service scene (SceneData asset)
2. Create ScopeBuilder from options
3. Create new GameObject with LifetimeScope
4. Move components to scope
5. Execute construction callback
6. Build VContainer container
7. Run EventLoop (construction phase)
8. Return ScopeLoadResult
```

### Stage 3: Event Loop (Lifecycle Management)

**File:** `Common/Events/EventLoop.cs`

**Construction Phase (IScopeSetup):**

```csharp
1. OnBaseSetup()          - Synchronous base setup
2. OnBaseSetupAsync()     - Async base setup
3. OnSetup()              - Synchronous setup
4. OnSetupAsync()         - Async setup
5. OnSetupCompletion()    - Sync completion
6. OnSetupCompletionAsync() - Async completion
```

**Loaded Phase (IScopeLoaded):**

```csharp
1. OnLoaded()             - Synchronous loaded callback
2. OnLoadedAsync()        - Async loaded callback
```

**Dispose Phase:**

```csharp
1. OnDispose()            - Synchronous cleanup
2. OnDisposeAsync()       - Async cleanup
```

### Stage 4: Entity Scope Loading

**File:** `Scopes/Entities/EntityScopeLoader.cs`

```
1. Create EntityBuilder
2. Find/spawn entity prefab
3. Get IScopeEntityView from entity
4. Build LifetimeScope for entity
5. Create child Lifetime from parent
6. Setup component registration
7. Execute EventLoop
8. Return EntityScopeResult
```

---

## VContainer Integration

**Custom RegistrationBuilder Wrapping**

```csharp
// Fluent API on top of VContainer
builder.Register<T>()
    .As<IInterface>()
    .WithParameter<IDep>(instance)
    .AsSelfResolvable();

// Translates to VContainer registration
containerBuilder.Register<T>()
    .As<IInterface>()
    .WithParameter(instance)
    .AsSelf();
```

**Component Auto-Injection**

```csharp
builder.RegisterComponent<UIElement>()
    .As<IUIService>();

// VContainer finds and injects components
// during scope loading
```

**Hierarchical LifetimeScope Management**

```
Root LifetimeScope (InternalScope)
  ├── Child LifetimeScope (Global)
  ├── Child LifetimeScope (Meta)
  └── Child LifetimeScope (GameLoop)
      ├── Ephemeral Child (MenuScope)
      └── Ephemeral Child (GamePlayScope)
```

---

## Key Interfaces

### Lifetime
```csharp
IReadOnlyLifetime, ILifetime, Lifetime, GameObjectLifetime
```

### Events
```csharp
IEventSource<T>, EventSource<T>, ILifetimedValue<T>, LifetimedValue<T>
```

### Properties
```csharp
IViewableProperty<T>, ViewableProperty<T>, ViewableList<T>, ViewableDictionary<K, V>
```

### Builders
```csharp
IBuilder, IScopeBuilder, IEntityBuilder, ScopeBuilder, EntityBuilder
```

### Registration
```csharp
IServiceCollection, ServiceCollection, RegistrationBuilder
```

### Scopes
```csharp
ILoadedScope, ISceneLoader, IEntityScopeLoader, IServiceScopeBinder
```

### Lifecycle Callbacks
```csharp
IScopeSetup, IScopeSetupAsync, IScopeLoaded, IScopeLoadedAsync
```

---

## Collection Safety

**ModifiableList<T> Pattern**

```csharp
public IEnumerator<T> GetEnumerator()
{
    foreach (var item in _list)  // Safe iteration
        yield return item;

    // After iteration, apply pending changes
    foreach (var add in _add)
        _list.Add(add);
    _add.Clear();

    foreach (var remove in _remove)
        _list.Remove(remove);
    _remove.Clear();
}
```

**Benefits:**

- Safe Add/Remove during iteration
- No collection mutation exceptions
- Clear, predictable behavior
- Performance: one pass through collection

---

## Key Files

| File | Purpose |
|------|---------|
| `Common/Reactive/Lifetimes/Lifetime.cs` | Core lifetime |
| `Common/Reactive/Events/LifetimedValue.cs` | Reactive property |
| `Setup/InternalScopeLoader.cs` | Bootstrap entry |
| `Scopes/Services/ServiceScopeLoader.cs` | Scope creation |
| `Scopes/Common/Services/ServiceCollection.cs` | Registration buffer |
| `Scopes/Common/Builders/BuilderExtensions.cs` | Fluent API |
| `Scopes/Entities/EntityBuilder.cs` | Entity DI |
| `Common/Events/EventLoop.cs` | Lifecycle callbacks |

---

## Critical Patterns

### Pattern 1: Automatic Cleanup

Every subscription is lifetime-aware:
```csharp
_property.Advise(lifetime, handler);
// Automatically removes handler when lifetime terminates
```

### Pattern 2: Hierarchical Cleanup

```
Parent Lifetime terminates
  └─ All child lifetimes terminate
  └─ All listeners removed
  └─ All resources freed
```

### Pattern 3: Value-Specific Lifetimes

```
new_value emitted
  ├─ Previous value lifetime terminates
  ├─ New lifetime created for subscribers
  └─ Old subscribers auto-cleanup
```

### Pattern 4: Fire-and-Forget with .Forget()

```csharp
AsyncOperation().Forget();  // Explicitly signals fire-and-forget
// Prevents unused task warning
```

---

## Integration Points

- **Global:** Uses for scope creation and asset environment
- **Meta:** Uses for lifetime-based backend projection cleanup
- **Loop:** Uses for scope loading and event loop
- **Menu/GamePlay:** Use lifetime management for entity cleanup
- **All modules:** Use reactive properties and event sources

---

## See Also

- `OVERVIEW.md` - Application architecture
- `COMMON_LIFETIMES.md` - Lifetime management patterns
- `COMMON_REACTIVE.md` - Reactive system patterns
- `CODE_STYLE.md` - Code organization rules
