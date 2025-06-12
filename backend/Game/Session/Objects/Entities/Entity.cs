using Common;
using Shared;

namespace Game;

public interface IEntity
{
    int Id { get; }
    IUser Owner { get; }
    IReadOnlyList<IObjectProperty> Properties { get; }
    IReadOnlyLifetime Lifetime { get; }

    void Destroy();
    INetworkContext CreateOverview();
}

public class Entity : IEntity
{
    public Entity(
        IUser owner,
        IReadOnlyList<IObjectProperty> properties,
        int id,
        byte[] payload)
    {
        _payload = payload;
        Owner = owner;
        Properties = properties;
        Id = id;

        _lifetime = owner.Lifetime.Child();
    }

    private readonly ILifetime _lifetime;
    private readonly byte[] _payload;

    public int Id { get; }
    public IUser Owner { get; }
    public IReadOnlyList<IObjectProperty> Properties { get; }
    public IReadOnlyLifetime Lifetime => _lifetime;

    public void Destroy()
    {
        _lifetime.Terminate();
    }

    public INetworkContext CreateOverview()
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
        
        var updatedContext = new EntityContexts.Overview()
        {
            EntityId = Id,
            Properties = properties,
            OwnerId = Owner.Index,
            Payload = _payload
        };
        
        return updatedContext;
    }
}