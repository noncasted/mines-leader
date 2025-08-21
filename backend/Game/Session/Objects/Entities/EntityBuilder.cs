using MemoryPack;
using Shared;

namespace Game;

public class EntityBuilder
{
    public EntityBuilder(
        ISessionUsers users,
        ISessionEntities entities,
        ISessionObjects objects,
        IPropertyUpdateSender propertyUpdateSender,
        IUser owner)
    {
        _users = users;
        _entities = entities;
        _objects = objects;
        _propertyUpdateSender = propertyUpdateSender;
        _owner = owner;

        _id = owner.Index * 1000_00 + entities.CountByUser(owner);
    }

    private readonly ISessionUsers _users;
    private readonly ISessionEntities _entities;
    private readonly ISessionObjects _objects;
    private readonly IPropertyUpdateSender _propertyUpdateSender;
    private readonly IUser _owner;
    private readonly Dictionary<int, IObjectProperty> _properties = new();
    private readonly int _id;

    private IEntityPayload? _payload;

    public EntityBuilder WithProperty(ObjectProperty property)
    {
        property.Construct(_propertyUpdateSender, _id);
        _properties.Add(property.Id, property);
        return this;
    }

    public ValueProperty<T> AddProperty<T>(int id) where T : new()
    {
        var property = new ValueProperty<T>(id);
        property.Construct(_propertyUpdateSender, _id);
        _properties.Add(id, property);
        return property;
    }

    public EntityBuilder WithPayload(IEntityPayload payload)
    {
        _payload = payload;
        return this;
    }

    public IEntity Build()
    {
        var payloadBytes = MemoryPackSerializer.Serialize(_payload!);
        var entity = new Entity(_owner, _properties, _id, payloadBytes);

        _entities.Add(entity);
        _objects.Add(entity);

        var overview = entity.CreateOverview();

        _users.SendAll(overview);

        return entity;
    }
}