using Common;
using Shared;

namespace Game;

public interface IService
{
    string Key { get; }
    IReadOnlyList<IObjectProperty> Properties { get; }
    IReadOnlyLifetime Lifetime { get; }

    ServiceContexts.Overview CreateOverview();
}

public class Service : IService
{
    public Service(string key, IReadOnlyLifetime lifetime, IReadOnlyList<IObjectProperty> properties)
    {
        Key = key;
        Lifetime = lifetime;
        Properties = properties;
    }

    public string Key { get; }
    public IReadOnlyList<IObjectProperty> Properties { get; }
    public IReadOnlyLifetime Lifetime { get; }

    public ServiceContexts.Overview CreateOverview()
    {
        var properties = new List<ObjectContexts.PropertyUpdate>();

        foreach (var property in Properties)
        {
            properties.Add(new ObjectContexts.PropertyUpdate()
            {
                Id   = property.Id,
                Value = property.Value
            });
        }
        
        var updatedContext = new ServiceContexts.Overview()
        {
            Key = Key,
            Properties = properties
        };
        
        return updatedContext;
    }
}