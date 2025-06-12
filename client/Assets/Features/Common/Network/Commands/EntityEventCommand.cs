using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityEventCommand : NetworkCommand<ObjectContexts.Event>
    {
        private readonly INetworkEntitiesCollection _networkEntities;

        public EntityEventCommand(INetworkEntitiesCollection networkEntities)
        {
            _networkEntities = networkEntities;
        }

        protected override UniTask Execute(IReadOnlyLifetime lifetime, ObjectContexts.Event context)
        {
            var entity = _networkEntities.Entries[context.EntityId];
            entity.Events.Invoke(context.Value);
            return UniTask.CompletedTask;
        }
    }
}