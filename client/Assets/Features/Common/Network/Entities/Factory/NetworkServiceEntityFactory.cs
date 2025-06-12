using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using Shared;

namespace Common.Network
{
    [MemoryPackable]
    public partial class NetworkServiceEntityPayload : IEntityPayload
    {
        public string Key { get; set; }
    }

    public class NetworkServiceEntityFactory : INetworkServiceEntityFactory
    {
        public NetworkServiceEntityFactory(
            INetworkEntityFactory factory,
            INetworkEntityDestroyer destroyer,
            INetworkEntitiesCollection entities,
            INetworkSender sender,
            INetworkUsersCollection users)
        {
            _factory = factory;
            _destroyer = destroyer;
            _entities = entities;
            _sender = sender;
            _users = users;

            _sessionUser = new NetworkUser(0, false, new Lifetime(), Guid.Empty);
        }

        private readonly INetworkEntityFactory _factory;
        private readonly INetworkEntityDestroyer _destroyer;
        private readonly INetworkEntitiesCollection _entities;
        private readonly INetworkSender _sender;
        private readonly INetworkUsersCollection _users;

        private readonly INetworkUser _sessionUser;

        public async UniTask<INetworkEntity> Create(
            IReadOnlyLifetime lifetime,
            string key,
            params INetworkProperty[] properties)
        {
            var rawProperties = new List<byte[]>();

            foreach (var property in properties)
                rawProperties.Add(property.Collect());

            var request = new EntityContexts.GetServiceRequest()
            {
                Key = key,
                Properties = rawProperties,
                Payload = MemoryPackSerializer.Serialize(new NetworkServiceEntityPayload()
                {
                    Key = key
                })
            };

            var response = await _sender.SendFull<EntityContexts.GetServiceResponse>(lifetime, request);

            var entityProperties = new NetworkEntityProperties();

            for (var index = 0; index < response.Properties.Count; index++)
            {
                var receivedProperty = response.Properties[index];
                var targetProperty = properties[index];

                targetProperty.Update(receivedProperty);
            }

            foreach (var property in properties)
                entityProperties.Add(property);

            var entity = new NetworkEntity(_sender, _destroyer, _sessionUser, response.EntityId, entityProperties);
            _entities.Add(entity);

            return entity;
        }
    }
}