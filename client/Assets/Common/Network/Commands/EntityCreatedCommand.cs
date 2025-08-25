using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityCreatedCommand : OneWayCommand<SharedSessionEntity.CreatedOverview>
    {
        private readonly INetworkEntityFactory _factory;
        private readonly INetworkUsersCollection _users;

        public EntityCreatedCommand(INetworkEntityFactory factory, INetworkUsersCollection users)
        {
            _factory = factory;
            _users = users;
        }

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionEntity.CreatedOverview context)
        {
            var owner = _users.Entries[context.OwnerId];

            var payload = new RemoteEntityData(owner, context.EntityId, context.Properties, context.Payload);

            _factory.CreateRemote(lifetime, payload).Forget();
        }
    }
}