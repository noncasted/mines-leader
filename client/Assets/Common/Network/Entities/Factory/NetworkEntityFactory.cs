using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using MemoryPack;
using Shared;

namespace Common.Network
{
    public class NetworkEntityFactory : INetworkEntityFactory
    {
        public NetworkEntityFactory(
            INetworkSocket socket,
            INetworkEntitiesCollection entities,
            INetworkObjectsCollection objects,
            INetworkUsersCollection users,
            INetworkEntityIds ids)
        {
            _socket = socket;
            _entities = entities;
            _objects = objects;
            _users = users;
            Ids = ids;
        }

        private readonly INetworkSocket _socket;
        private readonly INetworkEntitiesCollection _entities;
        private readonly INetworkObjectsCollection _objects;
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
            var properties = new List<ObjectContexts.PropertyUpdate>();

            foreach (var (id, property) in entity.Properties)
            {
                properties.Add(new ObjectContexts.PropertyUpdate()
                {
                    ObjectId = entity.Id,
                    PropertyId = id,
                    Value = property.Collect()
                });
            }

            var request = new EntityContexts.CreateRequest()
            {
                Id = entity.Id,
                Properties = properties,
                Payload = MemoryPackSerializer.Serialize(payload)
            };

            await _socket.SendFull<EntityContexts.CreateResponse>(request);

            _objects.Add(entity);
            _entities.Add(entity);
        }

        public async UniTask CreateRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            await UniTask.SwitchToMainThread();

            var payload = MemoryPackSerializer.Deserialize<IEntityPayload>(data.Payload);
            var entity = await _listeners[payload.GetType()].Invoke(lifetime, data);
            _entities.Add(entity);
            _objects.Add(entity);
        }
    }
}