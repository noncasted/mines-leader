using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class RemoteUserUpdateCommand : NetworkCommand<UserContexts.RemoteUpdate>
    {
        public RemoteUserUpdateCommand(
            INetworkUsersCollection users,
            INetworkSession session)
        {
            _users = users;
            _session = session;
        }

        private readonly INetworkUsersCollection _users;
        private readonly INetworkSession _session;

        protected override void Execute(IReadOnlyLifetime lifetime, UserContexts.RemoteUpdate context)
        {
            if (_users.Entries.ContainsKey(context.Index) == true)
                return;

            var user = new NetworkUser(context.Index, false, _session.Lifetime.Child(), context.BackendId);
            _users.Add(user);
        }
    }
}