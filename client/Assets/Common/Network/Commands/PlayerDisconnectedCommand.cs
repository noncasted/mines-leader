using Global.Backend;
using Internal;
using Shared;

namespace Common.Network
{
    public class PlayerDisconnectedCommand : OneWayCommand<UserContexts.RemoteDisconnect>
    {
        private readonly INetworkUsersCollection _users;

        public PlayerDisconnectedCommand(INetworkUsersCollection users)
        {
            _users = users;
        }

        protected override void Execute(IReadOnlyLifetime lifetime, UserContexts.RemoteDisconnect context)
        {
            _users.Entries[context.Index].DisposeRemote();
        }
    }
}