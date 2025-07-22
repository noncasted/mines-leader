using Common;
using Shared;

namespace Game;

public interface IEntity : IObject
{
    IUser Owner { get; }

    void Destroy();
    INetworkContext CreateOverview();
}

public class Entity : IEntity
{
    public Entity(
        IUser owner,
        IReadOnlyDictionary<int, IObjectProperty> properties,
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
    public IReadOnlyDictionary<int, IObjectProperty> Properties { get; }
    public IReadOnlyLifetime Lifetime => _lifetime;

    public void Destroy()
    {
        _lifetime.Terminate();
    }

    public INetworkContext CreateOverview()
    {
        var properties = new List<SharedSessionObject.PropertyUpdate>();

        foreach (var (_, property) in Properties)
        {
            properties.Add(new SharedSessionObject.PropertyUpdate()
            {
                PropertyId = property.Id,
                Value = property.Value
            });
        }

        var updatedContext = new SharedSessionEntity.CreatedOverview()
        {
            EntityId = Id,
            Properties = properties,
            OwnerId = Owner.Index,
            Payload = _payload
        };

        return updatedContext;
    }
}