using System;
using System.Linq;
using Shared;

namespace Game.Session
{
    // A frame between a guest of a P2P match and its host, for a guest without a direct connection: relayed to the
    // target as it is, stamped with the sender, never read.
    public class PeerTunnelCommand : Command<SharedSessionPeer.Tunnel>
    {
        public PeerTunnelCommand(ISessionUsers users, ISessionLog log)
        {
            _users = users;
            _log = log;
        }

        private readonly ISessionUsers _users;
        private readonly ISessionLog _log;

        protected override void Execute(IUser user, SharedSessionPeer.Tunnel context)
        {
            if (context.TargetIndex == user.Index)
                throw new InvalidOperationException($"[Session] [Peers] User {user.Index} tunnels to itself");

            var target = _users.FirstOrDefault(other => other.Index == context.TargetIndex);

            // The target has left while the frame was on its way: the tunnel is closed by its leave.
            if (target == null)
            {
                _log.Debug($"[Session] [Peers] Tunnel frame of {user.Index} to {context.TargetIndex}: the target is not in the session");
                return;
            }

            context.SenderIndex = user.Index;
            target.Send(context);
        }
    }
}
