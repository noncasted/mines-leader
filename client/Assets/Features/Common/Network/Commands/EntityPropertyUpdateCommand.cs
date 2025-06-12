using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityPropertyUpdateCommand : NetworkCommand<EntityContexts.PropertyUpdate>
    {
        private readonly INetworkEntitiesCollection _networkEntities;

        public EntityPropertyUpdateCommand(INetworkEntitiesCollection networkEntities)
        {
            _networkEntities = networkEntities;
        }
        
        protected override UniTask Execute(IReadOnlyLifetime lifetime, EntityContexts.PropertyUpdate context)
        {
            var entity = _networkEntities.Entries[context.EntityId];
            var property = entity.Properties[context.PropertyId];
            property.Update(context.Value);
            
            return UniTask.CompletedTask;    
        }
    }
}