using Shared;

namespace Game;

public interface IEntityFactory
{
    EntityBuilder Create(IUser owner);
    IEntity Create(SharedSessionEntity.CreateRequest request, IUser owner);
}

public class EntityFactory : IEntityFactory
{
    public EntityFactory(
        ISessionUsers users,
        ISessionEntities entities,
        ISessionObjects objects,
        IPropertyUpdateSender propertyUpdateSender)
    {
        _users = users;
        _entities = entities;
        _objects = objects;
        _propertyUpdateSender = propertyUpdateSender;
    }

    private readonly ISessionUsers _users;
    private readonly ISessionEntities _entities;
    private readonly ISessionObjects _objects;
    private readonly IPropertyUpdateSender _propertyUpdateSender;

    public EntityBuilder Create(IUser owner)
    {
        return new EntityBuilder(_users, _entities, _objects, _propertyUpdateSender, owner);
    }

    public IEntity Create(SharedSessionEntity.CreateRequest request, IUser owner)
    {
        var properties = new Dictionary<int, IObjectProperty>();

        foreach (var propertyRequest in request.Properties)
        {
            var property = new ObjectProperty(propertyRequest.PropertyId, propertyRequest.Value);
            property.Construct(_propertyUpdateSender, request.Id);
            properties.Add(propertyRequest.PropertyId, property);
        }

        var entity = new Entity(owner, properties, request.Id, request.Payload);

        _entities.Add(entity);
        _objects.Add(entity);

        return entity;
    }
}