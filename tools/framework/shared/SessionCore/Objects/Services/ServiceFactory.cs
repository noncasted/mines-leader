using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

namespace Game.Session
{
    public interface IServiceFactory
    {
        // The services of the container (the game logic of the session) are set up and registered: by the host before
        // the users come, in the session queue of the backend.
        void Setup();

        // A service by its key: of the container, or an empty one created by the first request.
        IService GetOrCreate(SharedSessionService.GetRequest request);
    }

    public class ServiceFactory : IServiceFactory
    {
        public ServiceFactory(
            IEnumerable<IService> services,
            ISessionServices collection,
            ISessionData data,
            ISessionObjects objects)
        {
            _services = services;
            _collection = collection;
            _data = data;
            _objects = objects;
        }

        private readonly IEnumerable<IService> _services;
        private readonly ISessionServices _collection;
        private readonly ISessionData _data;
        private readonly ISessionObjects _objects;

        public void Setup()
        {
            foreach (var service in _services)
            {
                service.Setup(_data.Lifetime);
                Register(service);
            }
        }

        public IService GetOrCreate(SharedSessionService.GetRequest request)
        {
            if (_collection.Entries.TryGetValue(request.Key, out var existing))
                return existing;

            var service = new Service(request.Key);

            foreach (var propertyId in request.PropertiesIds)
                service.BindProperty(new ObjectProperty(propertyId, Array.Empty<byte>()));

            service.Setup(_data.Lifetime);
            Register(service);

            return service;
        }

        // Both or none: the objects refuse an id taken by another key (Service.GetId) before the key is registered, so
        // a refused service is not found by a later request.
        private void Register(IService service)
        {
            if (_collection.Entries.ContainsKey(service.Key) == true)
                throw new InvalidOperationException($"Service {service.Key} is already registered");

            _objects.Add(service);
            _collection.Add(service);
        }
    }
}
