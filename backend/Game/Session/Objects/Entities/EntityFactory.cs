using Shared;

namespace Game;

public interface IEntityFactory
{
    IEntity Create(EntityContexts.CreateRequest request, IUser user);
}

public class EntityFactory : IEntityFactory
{
    public EntityFactory(ISessionEntities collection, ISessionObjects objects)
    {
        _collection = collection;
        _objects = objects;
    }

    private readonly ISessionEntities _collection;
    private readonly ISessionObjects _objects;

    public IEntity Create(EntityContexts.CreateRequest request, IUser user)
    {
        var properties = new Dictionary<int, IObjectProperty>();

        foreach (var property in request.Properties)
            properties.Add(property.PropertyId, new ObjectProperty(property.PropertyId, property.Value));

        var entity = new Entity(user, properties, request.Id, request.Payload);

        _collection.Add(entity);
        _objects.Add(entity);

        return entity;
    }
}