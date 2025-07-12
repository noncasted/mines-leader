using Internal;
using Shared;

namespace Common.Network
{
    public class LocalUserUpdateCommand : NetworkCommand<UserContexts.LocalUpdate>
    {
        public LocalUserUpdateCommand(
            INetworkUsersCollection users,
            INetworkSession session)
        {
            _users = users;
            _session = session;
        }

        private readonly INetworkUsersCollection _users;
        private readonly INetworkSession _session;

        protected override void Execute(IReadOnlyLifetime lifetime, UserContexts.LocalUpdate context)
        {
            var user = new NetworkUser(context.Index, true, _session.Lifetime.Child(), _session.LocalUserId);
            _users.Add(user);
        }
    }
}