using Common;
using Shared;

namespace Game;

public interface IService : IObject
{
    string Key { get; }

    void Setup(IReadOnlyLifetime lifetime, IPropertyUpdateSender propertyUpdateSender);
    SharedSessionService.Overview CreateOverview();
}

public class Service : IService
{
    public Service(string key)
    {
        Key = key;
        Id = key.GetHashCode();
    }

    private readonly Dictionary<int, IObjectProperty> _properties = new();

    private IReadOnlyLifetime _lifetime = new TerminatedLifetime();

    public string Key { get; }
    public IReadOnlyDictionary<int, IObjectProperty> Properties => _properties;
    public int Id { get; }
    public IReadOnlyLifetime Lifetime => _lifetime;

    public void Setup(IReadOnlyLifetime lifetime, IPropertyUpdateSender propertyUpdateSender)
    {
        _lifetime = lifetime;

        foreach (var (_, property) in _properties)
            property.Construct(propertyUpdateSender, Id);
        
        OnStarted(lifetime);
    }
    
    public void BindProperty(IObjectProperty property)
    {
        if (_properties.ContainsKey(property.Id))
            throw new InvalidOperationException($"Property with index {property.Id} already exists.");

        _properties.Add(property.Id, property);
    }

    public SharedSessionService.Overview CreateOverview()
    {
        var properties = new List<SharedSessionObject.PropertyUpdate>();

        foreach (var (_, property) in Properties)
        {
            properties.Add(new SharedSessionObject.PropertyUpdate()
                {
                    PropertyId = property.Id,
                    Value = property.RawValue
                }
            );
        }

        var updatedContext = new SharedSessionService.Overview()
        {
            Key = Key,
            Properties = properties
        };

        return updatedContext;
    }

    protected virtual void OnStarted(IReadOnlyLifetime lifetime)
    {
    }
}