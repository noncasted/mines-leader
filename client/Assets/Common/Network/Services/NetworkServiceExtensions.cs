using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;

namespace Common.Network
{
    public static class NetworkServiceExtensions
    {
        public static NetworkServiceBuilder AddNetworkService<T>(this IScopeBuilder builder, string key)
            where T : NetworkService
        {
            var registration = builder.Register<T>();
            var serviceBuilder = new NetworkServiceBuilder(builder, key, registration);
            serviceBuilder.Setup<T>();

            return serviceBuilder;
        }

        public class NetworkServiceBuilder
        {
            public NetworkServiceBuilder(
                IScopeBuilder builder,
                string key,
                IRegistration registration)
            {
                _id = key.GetHashCode();
                _builder = builder;
                _key = key;

                Registration = registration;
            }

            private readonly IScopeBuilder _builder;
            private readonly string _key;
            private readonly int _id;

            private readonly Dictionary<int, INetworkProperty> _properties = new();

            private int _propertiesIndex;

            public readonly IRegistration Registration;

            public void Setup<T>() where T : NetworkService
            {
                _builder.Register<NetworkServiceResolver<T>>()
                    .WithParameter<IReadOnlyDictionary<int, INetworkProperty>>(_properties)
                    .WithParameter(_key)
                    .AsSessionCallback<NetworkServiceResolver<T>, INetworkSessionSetupCompleted>();
            }

            public NetworkServiceBuilder WithProperty<T>(int propertyId = 0) where T : class, new()
            {
                if (propertyId == 0)
                {
                    _propertiesIndex++;
                    propertyId = _propertiesIndex;
                }

                var property = new NetworkProperty<T>(_id);
                Registration.WithParameter(property);
                _properties.Add(propertyId, property);

                return this;
            }
        }

        public class NetworkServiceResolver<T> : INetworkSessionSetupCompleted where T : NetworkService
        {
            public NetworkServiceResolver(
                INetworkObjectsCollection objectsCollection,
                INetworkConnection connection,
                IReadOnlyDictionary<int, INetworkProperty> properties,
                T service,
                string key)
            {
                _objectsCollection = objectsCollection;
                _connection = connection;
                _properties = properties;
                _service = service;
                _key = key;
            }

            private readonly INetworkObjectsCollection _objectsCollection;
            private readonly INetworkConnection _connection;
            private readonly IReadOnlyDictionary<int, INetworkProperty> _properties;
            private readonly T _service;
            private readonly string _key;

            public async UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime)
            {
                var propertiesIds = _properties.Select(x => x.Key).ToList();

                var request = new SharedSessionService.GetRequest()
                {
                    Key = _key,
                    PropertiesIds = propertiesIds
                };

                var response = await _connection.Request<SharedSessionService.GetResponse>(request);

                for (var index = 0; index < response.Properties.Count; index++)
                {
                    var receivedProperty = response.Properties[index];
                    var targetProperty = _properties[receivedProperty.PropertyId];

                    if (receivedProperty.Value.Length != 0)
                        targetProperty.Update(receivedProperty.Value);
                }

                var events = new NetworkEvents(_connection, _service);

                var data = new NetworkServiceData(
                    response.Id,
                    _key,
                    _properties,
                    events,
                    lifetime);

                _service.Start(data);
                _objectsCollection.Add(_service);
            }
        }
    }
}