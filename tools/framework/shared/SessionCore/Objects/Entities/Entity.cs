using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

namespace Game.Session
{
    public interface IEntity : IObject
    {
        // The seat of the user who created the entity, or the owner of an entity built by the backend (it lives while the
        // seat does: a lost connection keeps it for the reconnect window); null for a session entity (ISessionEntityPayload): it belongs to the
        // session, lives until the session ends and is not destroyed when its creator leaves.
        ISessionSeat Owner { get; }

        // Owner index sent to the clients: the user index or SessionOwnerIndex for a session entity.
        int OwnerIndex { get; }
        IEntityPayload Payload { get; }

        void Destroy();
        INetworkContext CreateOverview();
    }

    public class Entity : IEntity
    {
        public Entity(
            ISessionSeat owner,
            IReadOnlyLifetime parentLifetime,
            IReadOnlyDictionary<int, IObjectProperty> properties,
            int id,
            IEntityPayload payload)
        {
            _payload = payload;
            Owner = owner;
            OwnerIndex = owner?.Index ?? SessionOwnerIndex;
            Properties = properties;
            Id = id;

            _lifetime = parentLifetime.Child();
        }

        // User indices start from 1 (SessionUsers.GetNextIndex): 0 is the session itself.
        public const int SessionOwnerIndex = 0;

        private readonly ILifetime _lifetime;
        private readonly IEntityPayload _payload;

        public int Id { get; }
        public ISessionSeat Owner { get; }
        public int OwnerIndex { get; }
        public IEntityPayload Payload => _payload;
        public IReadOnlyDictionary<int, IObjectProperty> Properties { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public void Destroy()
        {
            _lifetime.Terminate();
        }

        public INetworkContext CreateOverview()
        {
            var properties = new List<SharedSessionObject.PropertyUpdate>();

            foreach (var (_, property) in Properties)
            {
                properties.Add(new SharedSessionObject.PropertyUpdate()
                {
                    PropertyId = property.Id,
                    Value = property.RawValue,
                    Version = property.Version
                });
            }

            var updatedContext = new SharedSessionEntity.CreatedOverview()
            {
                EntityId = Id,
                Properties = properties,
                OwnerId = OwnerIndex,
                Payload = _payload
            };

            return updatedContext;
        }
    }
}
