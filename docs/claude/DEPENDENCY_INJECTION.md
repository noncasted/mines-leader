# DEPENDENCY_INJECTION.md

This project uses a sophisticated dependency injection (DI) system built on top of VContainer with custom scope management and service registration patterns.

## Core DI Components

### ServiceCollection (`Assets/Internal/Scopes/Common/Services/ServiceCollection.cs:7`)
- **Purpose**: Central registry for service registrations and dependency injection
- **Key Methods**:
  - `AddSelfResolvable()` - Registers services that auto-resolve themselves during scope initialization
  - `AddBuilder()` - Registers services with custom registration builders
  - `Inject<T>()` - Manually injects existing component instances into the DI container
  - `PassRegistrations()` - Transfers registrations to VContainer's IContainerBuilder
  - `Resolve()` - Resolves self-resolvable services and injects component instances

### ScopeBuilder (`Assets/Internal/Scopes/Services/Builder/ScopeBuilder.cs:3`)
- **Purpose**: Main builder for service scopes with access to all scope dependencies
- **Key Properties**:
  - `Services` - ServiceCollection for registering services
  - `Assets` - Asset environment for loading resources
  - `SceneLoader` - Scene loading capabilities
  - `Binder` - Unity object hierarchy management
  - `ScopeLifetime` - Lifetime management for scope cleanup
  - `Events` - Event handling within scope
  - `Parent` - Parent scope for hierarchical DI

### EntityBuilder (`Assets/Internal/Scopes/Entities/EntityBuilder.cs:6`)
- **Purpose**: Specialized builder for entity-based scopes with VContainer LifetimeScope integration
- **Key Properties**:
  - `Services` - ServiceCollection for entity services
  - `Scope` - VContainer LifetimeScope for entity container
  - `View` - Entity view component for Unity integration
  - **Extension**: `Get<T>()` method for direct service resolution from entity scope

### ISceneService (`Assets/Internal/Scopes/Services/SceneServices/ISceneService.cs:3`)
- **Purpose**: Interface for scene-specific service registration
- **Usage**: Implement this interface to create services that register themselves when a scene loads
- **Method**: `Create(IScopeBuilder builder)` - Called during scene initialization to register services

## Key Interfaces

### IServiceCollection (`Assets/Internal/Scopes/Common/Services/Abstract/IServiceCollection.cs:5`)
- Service registration contract with three registration patterns:
  - Self-resolvable services (auto-instantiated)
  - Builder-based services (custom registration logic)
  - Instance injection (pre-created objects)

### IBuilder (`Assets/Internal/Scopes/Common/Builders/IBuilder.cs:3`)
- Base interface for all builders providing core dependencies:
  - `Services` - Service registration
  - `Assets` - Asset loading
  - `Events` - Event system
  - `Lifetime` - Resource cleanup

### IScopeBuilder (`Assets/Internal/Scopes/Services/Builder/IScopeBuilder.cs:3`)
- Extended builder interface adding:
  - `SceneLoader` - Scene management
  - `Binder` - Unity object binding
  - `ScopeLifetime` - Full lifetime control
  - `IsMock` - Testing support

### IEntityBuilder (`Assets/Internal/Scopes/Entities/Abstract/IEntityBuilder.cs:5`)
- Entity-specific builder interface adding:
  - `Scope` - VContainer LifetimeScope
  - `ScopeLifetime` - Entity lifetime management
  - `View` - Entity view component

### IServiceScopeLoader (`Assets/Internal/Scopes/Services/IServiceScopeLoader.cs:6`)
- Handles loading of service scopes asynchronously
- Uses `ScopeLoadOptions` for configuration
- Returns `ILoadedScope` with lifetime management

## Common Usage Patterns

### Service Registration
```csharp
// Self-resolvable service
builder.Services.AddSelfResolvable(registrationBuilder);

// Custom registration
builder.Services.AddBuilder(registrationBuilder);

// Instance injection
builder.Services.Inject(existingInstance);
```

### Scene Service Implementation
```csharp
public class MySceneService : MonoBehaviour, IMyService, ISceneService
{
    [SerializeField] private GameObject _someGameObject;
    
    private readonly Dictionary<int, string> _cache = new();
    
    public string CurrentValue { get; private set; }
    
    [Inject]
    private void Construct(IOtherService otherService)
    {
        // Dependency injection happens automatically
    }
    
    public void Create(IScopeBuilder builder)
    {
        // Register this component with its interface
        builder.RegisterComponent(this).As<IMyService>();
    }
    
    public void DoSomething()
    {
        // Public methods
    }
    
    private void HelperMethod()
    {
        // Private methods
    }
}
```

### Alternative Registration Methods
```csharp
// For non-component services
public void Create(IScopeBuilder builder)
{
    builder.RegisterInstance(someService);
    builder.RegisterSingleton<IMyService, MyService>();
}
```

### Scope Building Extensions (`Assets/Internal/Scopes/Services/Builder/ScopeBuilderExtensions.cs:7`)
- `FindOrLoadScene<TComponent>()` - Load scene and find component
- `FindOrLoadSceneWithServices<TScene>()` - Load scene and auto-register services
- `RegisterScriptableRegistry<T1, T2>()` - Register asset registries
- `Instantiate<T>()` - Create Unity objects with proper hierarchy binding

### Asset Integration
- Builders provide `Assets` property for accessing `IAssetEnvironment`
- Use `builder.GetAsset<T>()` to load ScriptableObject assets
- Assets are automatically available to all services in the scope

### Lifetime Management
- All builders provide `Lifetime` property for resource cleanup
- Services should subscribe to lifetime events for proper disposal
- Entity scopes have dedicated `ScopeLifetime` for full lifetime control

### Hierarchical Scopes
- Service scopes can have parent-child relationships
- Child scopes inherit services from parent scopes
- Use `Parent` property to access parent scope services

### Testing Support
- `IsMock` property indicates testing environment
- Mock scopes can bypass certain initialization logic
- Enables unit testing of service registration logic

## VContainer Integration

### Registration Patterns
- Uses VContainer's `RegistrationBuilder` for service definitions
- Supports VContainer lifetime management (Singleton, Transient, etc.)
- Integrates with VContainer's `IContainerBuilder` and `IObjectResolver`

### Unity Integration
- `EntityBuilder` works with VContainer's `LifetimeScope` components
- `IServiceScopeBinder` manages Unity object hierarchy
- Automatic parent-child relationships with Unity's DI container hierarchy

### Async Loading
- Service scope loading is async using UniTask
- Supports complex initialization sequences
- Proper cleanup and error handling in async scenarios

## Error Prevention

### Common Issues
- **Null Reference**: Always check components before calling `Inject<T>()`
- **Missing Dependencies**: Ensure parent scopes are loaded before child scopes
- **Lifetime Leaks**: Subscribe to lifetime events for proper cleanup
- **Scene Loading**: Use scope-aware scene loading methods instead of Unity's direct scene loading

### Best Practices
- Register services in dependency order (dependencies first)
- Use lifetime management for all resources requiring cleanup
- Implement ISceneService for scene-specific services
- Use `public void Create(IScopeBuilder builder)` method, not explicit interface implementation
- Use `builder.RegisterComponent(this).As<IInterface>()` for MonoBehaviour services
- Use `[Inject]` methods for dependency injection, not manual initialization
- Test with mock scopes to verify registration logic

### Class Member Ordering
Follow this strict order in all C# classes:
1. **Constructors** (if any)
2. **`[SerializeField]` fields** - Unity serialized fields
3. **`readonly` fields** - Immutable fields and collections
4. **Public properties** - Exposed properties with getters/setters
5. **`[Inject]` Construct methods** - Dependency injection methods
6. **`ISceneService.Create()` method** - Service registration method
7. **All public methods** - Public interface methods
8. **All private methods** - Internal implementation methods

### Common Registration Patterns
```csharp
// MonoBehaviour component with interface
builder.RegisterComponent(this).As<IMyService>();

// Multiple interfaces
builder.RegisterComponent(this).As<IMyService>().As<IAnotherInterface>();

// Self-registration only
builder.RegisterComponent(this);

// Instance registration
builder.RegisterInstance<IService>(serviceInstance);
```