using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using Shared;

namespace Common.Network
{
    public class NetworkEntityFactory : INetworkEntityFactory
    {
        public NetworkEntityFactory(
            INetworkSender sender,
            INetworkEntitiesCollection entities,
            INetworkUsersCollection users,
            INetworkEntityIds ids)
        {
            _sender = sender;
            _entities = entities;
            _users = users;
            Ids = ids;
        }

        private readonly INetworkSender _sender;
        private readonly INetworkEntitiesCollection _entities;
        private readonly INetworkUsersCollection _users;

        private readonly Dictionary<Type, Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>>>
            _listeners = new();

        public INetworkUser LocalUser => _users.Local;
        public INetworkEntityIds Ids { get; }

        public void ListenRemote<T>(
            IReadOnlyLifetime lifetime,
            Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>> listener)
        {
            var type = typeof(T);
            _listeners.Add(type, listener);
            lifetime.Listen(() => _listeners.Remove(type));
        }

        public async UniTask Send(IReadOnlyLifetime lifetime, INetworkEntity entity, IEntityPayload payload)
        {
            var properties = new List<byte[]>();

            foreach (var property in entity.Properties)
            {
                property.Construct(entity);
                properties.Add(property.Collect());
            }

            var request = new EntityContexts.CreateRequest()
            {
                Id = entity.Id,
                Properties = properties,
                Payload = MemoryPackSerializer.Serialize(payload)
            };

            var response = await _sender.SendFull<EntityContexts.CreateResponse>(lifetime, request);
            _entities.Add(entity);
        }

        public async UniTask CreateRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            await UniTask.SwitchToMainThread();

            var payload = MemoryPackSerializer.Deserialize<IEntityPayload>(data.Payload);
            var entity = await _listeners[payload.GetType()].Invoke(lifetime, data);
            _entities.Add(entity);
        }
    }
}