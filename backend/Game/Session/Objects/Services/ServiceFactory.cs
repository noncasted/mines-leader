using Shared;

namespace Game;

public interface IServiceFactory
{
    void Add(Service service);
    IService GetOrCreate(SharedSessionService.GetRequest request);
}

public class ServiceFactory : IServiceFactory
{
    public ServiceFactory(
        ISessionServices collection,
        ISessionData data,
        ISessionObjects objects,
        IPropertyUpdateSender propertyUpdateSender
    )
    {
        _collection = collection;
        _data = data;
        _objects = objects;
        _propertyUpdateSender = propertyUpdateSender;
    }

    private readonly ISessionServices _collection;
    private readonly ISessionData _data;
    private readonly ISessionObjects _objects;
    private readonly IPropertyUpdateSender _propertyUpdateSender;

    public void Add(Service service)
    {
        if (_collection.Entries.ContainsKey(service.Key))
            throw new InvalidOperationException($"Service with key {service.Key} already exists.");

        service.Setup(_data.Lifetime);

        _collection.Add(service);
        _objects.Add(service);
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

        service.Setup(_data.Lifetime);

        _collection.Add(service);
        _objects.Add(service);

        return service;
    }
}