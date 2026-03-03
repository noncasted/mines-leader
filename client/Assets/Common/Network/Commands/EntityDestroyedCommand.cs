using Internal;
using Shared;
using UnityEngine;

namespace Common.Network
{
    public class EntityDestroyedCommand : OneWayCommand<SharedSessionEntity.DestroyUpdate>
    {
        private readonly INetworkEntitiesCollection _entities;

        public EntityDestroyedCommand(INetworkEntitiesCollection entities)
        {
            _entities = entities;
        }

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionEntity.DestroyUpdate context)
        {
            var entity = _entities.Entries[context.EntityId];
            Debug.Log($"[Network] Destroying entity with ID: {context.EntityId} owner ID: {entity.Owner.BackendId}");
            entity.DestroyRemote();
        }
    }
}