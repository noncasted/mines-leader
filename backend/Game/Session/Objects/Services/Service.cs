using Common;
using Shared;

namespace Game;

public interface IService : IObject
{
    string Key { get; }

    ServiceContexts.Overview CreateOverview();
}

public class Service : IService
{
    public Service(
        string key,
        IReadOnlyLifetime lifetime,
        IReadOnlyDictionary<int, IObjectProperty> properties)
    {
        Key = key;
        Id = key.GetHashCode();
        Lifetime = lifetime;
        Properties = properties;
    }

    public string Key { get; }
    public IReadOnlyDictionary<int, IObjectProperty> Properties { get; }
    public int Id { get; }
    public IReadOnlyLifetime Lifetime { get; }

    public ServiceContexts.Overview CreateOverview()
    {
        var properties = new List<ObjectContexts.PropertyUpdate>();

        foreach (var (_, property) in Properties)
        {
            properties.Add(new ObjectContexts.PropertyUpdate()
            {
                PropertyId = property.Id,
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