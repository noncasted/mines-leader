using Shared;

namespace Game;

public interface IServiceFactory
{
    IService GetOrCreate(ServiceContexts.GetRequest request);
}

public class ServiceFactory : IServiceFactory
{
    public ServiceFactory(ISessionServices collection, ISessionData data, ISessionObjects objects)
    {
        _collection = collection;
        _data = data;
        _objects = objects;
    }

    private readonly ISessionServices _collection;
    private readonly ISessionData _data;
    private readonly ISessionObjects _objects;

    public IService GetOrCreate(ServiceContexts.GetRequest request)
    {
        if (_collection.Entries.TryGetValue(request.Key, out var existing))
            return existing;

        var properties = new Dictionary<int, IObjectProperty>();

        foreach (var propertyId in request.PropertiesIds)
            properties.Add(propertyId, new ObjectProperty(propertyId, []));

        var service = new Service(request.Key, _data.Lifetime, properties);

        _collection.Add(service);
        _objects.Add(service);

        return service;
    }
}