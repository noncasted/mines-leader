using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    // The negotiation of a direct connection between two users: relayed to the target as it is, stamped with the sender.
    public class PeerSignalCommand : Command<SharedSessionPeer.Signal>
    {
        public PeerSignalCommand(ISessionUsers users, ISessionLog log)
        {
            _users = users;
            _log = log;
        }

        private readonly ISessionUsers _users;
        private readonly ISessionLog _log;

        protected override void Execute(IUser user, SharedSessionPeer.Signal context)
        {
            if (context.TargetIndex == user.Index)
                throw new InvalidOperationException($"[Session] [Peers] User {user.Index} signals itself");

            var target = _users.FirstOrDefault(other => other.Index == context.TargetIndex);

            // The target has left while the signal was on its way: the attempt of the sender times out and it plays
            // through the server.
            if (target == null)
            {
                _log.Info($"[Session] [Peers] Signal of {user.Index} to {context.TargetIndex}: the target is not in the session");
                return;
            }

            context.SenderIndex = user.Index;
            target.Send(context);
        }
    }
}
