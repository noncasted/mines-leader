using Global.Backend;
using Internal;
using Shared;

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
            entity.DestroyRemote();
        }
    }
}