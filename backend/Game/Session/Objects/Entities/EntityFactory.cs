using Shared;

namespace Game;

public interface IEntityFactory
{
    IEntity Create(EntityContexts.CreateRequest request, IUser user);
}

public class EntityFactory : IEntityFactory
{
    public EntityFactory(ISessionEntities collection, ISessionProperties properties)
    {
        _collection = collection;
        _properties = properties;
    }

    private readonly ISessionEntities _collection;
    private readonly ISessionProperties _properties;

    public IEntity Create(EntityContexts.CreateRequest request, IUser user)
    {
        var properties = new List<IObjectProperty>();

        foreach (var property in request.Properties)
            properties.Add(new ObjectProperty(property.Id, property.Value));
        
        var entity = new Entity(user, properties, request.Id, request.Payload);

        _collection.Add(entity);

        return entity;
    }
}