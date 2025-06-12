using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;

namespace Common.Network
{
    public class LocalUserUpdateCommand : NetworkCommand<UserContexts.LocalUpdate>
    {
        public LocalUserUpdateCommand(
            INetworkUsersCollection users,
            INetworkSession session,
            IBackendUser backendUser)
        {
            _users = users;
            _session = session;
            _backendUser = backendUser;
        }

        private readonly INetworkUsersCollection _users;
        private readonly INetworkSession _session;
        private readonly IBackendUser _backendUser;

        protected override UniTask Execute(IReadOnlyLifetime lifetime, UserContexts.LocalUpdate context)
        {
            var user = new NetworkUser(context.Index, true, _session.Lifetime.Child(), _backendUser.Id);
            _users.Add(user);
            
            return UniTask.CompletedTask;
        }

    }
}