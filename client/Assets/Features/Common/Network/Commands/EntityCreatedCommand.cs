using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityCreatedCommand : NetworkCommand<EntityContexts.CreateUpdate>
    {
        private readonly INetworkEntityFactory _factory;
        private readonly INetworkUsersCollection _users;

        public EntityCreatedCommand(INetworkEntityFactory factory, INetworkUsersCollection users)
        {
            _factory = factory;
            _users = users;
        }
        
        protected override UniTask Execute(IReadOnlyLifetime lifetime, EntityContexts.CreateUpdate context)
        {
            if (context.OwnerId == 0)
                return UniTask.CompletedTask;
            
            var owner = _users.Entries[context.OwnerId];

            var payload = new RemoteEntityData(owner, context.EntityId, context.Properties, context.Payload);
            
            return _factory.CreateRemote(lifetime, payload);
        }
    }
}