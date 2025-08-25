using Common;
using Shared;

namespace Game;

public interface IServiceFactory
{
    IService GetOrCreate(SharedSessionService.GetRequest request);
}

public class ServiceFactory : IServiceFactory, ISessionCreated
{
    public ServiceFactory(
        IEnumerable<IService> services,
        ISessionServices collection,
        ISessionData data,
        ISessionObjects objects,
        IPropertyUpdateSender propertyUpdateSender)
    {
        _services = services;
        _collection = collection;
        _data = data;
        _objects = objects;
        _propertyUpdateSender = propertyUpdateSender;
    }

    private readonly IEnumerable<IService> _services;
    private readonly ISessionServices _collection;
    private readonly ISessionData _data;
    private readonly ISessionObjects _objects;
    private readonly IPropertyUpdateSender _propertyUpdateSender;

    public Task OnSessionCreated(IReadOnlyLifetime lifetime)
    {
        foreach (var service in _services)
        {
            service.Setup(_data.Lifetime, _propertyUpdateSender);
            _collection.Add(service);
            _objects.Add(service);
        }

        return Task.CompletedTask;
    }
    
    public IService GetOrCreate(SharedSessionService.GetRequest request)
    {
        if (_collection.Entries.TryGetValue(request.Key, out var existing))
            return existing;

        var properties = new Dictionary<int, IObjectProperty>();
        var serviceId = request.Key.GetHashCode();

        foreach (var propertyId in request.PropertiesIds)
        {
            var property = new ObjectProperty(propertyId, []);
            property.Construct(_propertyUpdateSender, serviceId);
            properties.Add(propertyId, property);
        }

        var service = new Service(request.Key);

        foreach (var (_, property) in properties)
            service.BindProperty(property);

        service.Setup(_data.Lifetime, _propertyUpdateSender);

        _collection.Add(service);
        _objects.Add(service);

        return service;
    }
}