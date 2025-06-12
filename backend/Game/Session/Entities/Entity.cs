using Common;
using Shared;

namespace Game;

public class Entity : IEntity
{
    public Entity(
        IUser owner,
        IReadOnlyList<IEntityProperty> properties,
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
    public IReadOnlyList<IEntityProperty> Properties { get; }
    public IReadOnlyLifetime Lifetime => _lifetime;

    public void Destroy()
    {
        _lifetime.Terminate();
    }

    public INetworkContext GetUpdateContext()
    {
        var properties = new List<byte[]>();
        
        for (var i = 0; i < Properties.Count; i++)
        {
            var property = Properties[i];
            properties.Add(property.Value);
        }
        
        var updatedContext = new EntityContexts.CreateUpdate()
        {
            EntityId = Id,
            Properties = properties,
            OwnerId = Owner.Index,
            Payload = _payload
        };
        
        return updatedContext;
    }
}