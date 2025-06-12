using Common;
using Shared;

namespace Game;

public class EntityFactory : IEntityFactory
{
    public EntityFactory(ISessionEntities collection, IReadOnlyLifetime sessionLifetime)
    {
        _collection = collection;

        _sessionUser = new User()
        {
            Id = Guid.Empty,
            Index = 0,
            Lifetime = sessionLifetime.Child(),
            Reader = null,
            Writer = null
        };
    }

    private readonly ISessionEntities _collection;
    private readonly User _sessionUser;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private int _serviceIndex;

    public IEntity Create(EntityContexts.CreateRequest request, IUser user)
    {
        var properties = new List<IEntityProperty>();

        for (var i = 0; i < request.Properties.Count; i++)
        {
            var propertyValue = request.Properties[i];
            properties.Add(new EntityProperty(i, propertyValue));
        }

        var entity = new Entity(user, properties, request.Id, request.Payload);

        _collection.Add(entity);

        return entity;
    }

    public IEntity GetOrCreateService(EntityContexts.GetServiceRequest request)
    {
        _lock.Wait();

        try
        {
            if (_collection.ServiceEntities.TryGetValue(request.Key, out var existing))
                return existing;

            var properties = new List<IEntityProperty>();

            for (var i = 0; i < request.Properties.Count; i++)
            {
                var propertyValue = request.Properties[i];
                properties.Add(new EntityProperty(i, propertyValue));
            }

            _serviceIndex++;

            var entity = new Entity(_sessionUser, properties, _serviceIndex, request.Payload);

            _collection.Add(entity);
            _collection.AddService(request.Key, entity);
            return entity;
        }
        finally
        {
            _lock.Release();
        }
    }
}