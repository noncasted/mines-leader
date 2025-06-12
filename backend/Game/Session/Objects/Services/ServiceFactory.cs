using Common;
using Shared;

namespace Game;

public interface IServiceFactory
{
    IService GetOrCreate(ServiceContexts.GetRequest request);
}

public class ServiceFactory : IServiceFactory
{
    public ServiceFactory(ISessionServices collection, IReadOnlyLifetime sessionLifetime)
    {
        _collection = collection;
        _sessionLifetime = sessionLifetime;
    }

    private readonly ISessionServices _collection;
    private readonly IReadOnlyLifetime _sessionLifetime;

    public IService GetOrCreate(ServiceContexts.GetRequest request)
    {
        if (_collection.Entries.TryGetValue(request.Key, out var existing))
            return existing;

        var properties = new List<IObjectProperty>();

        foreach (var propertyId in request.PropertiesIds)
            properties.Add(new ObjectProperty(propertyId, []));

        var service = new Service(request.Key, _sessionLifetime, properties);

        _collection.Add(service);
        
        return service;
    }
}