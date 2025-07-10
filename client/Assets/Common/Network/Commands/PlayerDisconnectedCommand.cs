using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class PlayerDisconnectedCommand : NetworkCommand<UserContexts.RemoteDisconnect>
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