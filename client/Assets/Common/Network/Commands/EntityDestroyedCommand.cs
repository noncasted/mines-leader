using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityDestroyedCommand : NetworkCommand<EntityContexts.DestroyUpdate>
    {
        private readonly INetworkEntitiesCollection _entities;

        public EntityDestroyedCommand(INetworkEntitiesCollection entities)
        {
            _entities = entities;
        }

        protected override UniTask Execute(IReadOnlyLifetime lifetime, EntityContexts.DestroyUpdate context)
        {
            var entity = _entities.Entries[context.EntityId];
            entity.DestroyRemote();
            
            return UniTask.CompletedTask;
        }
    }
}