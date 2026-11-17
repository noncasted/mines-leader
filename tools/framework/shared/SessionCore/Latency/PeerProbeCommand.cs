using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Shared;

namespace Game.Session
{
    public class PeerProbeCommand : Command<SharedSessionLatency.PeerProbe>
    {
        public PeerProbeCommand(ISessionUsers users)
        {
            _users = users;
        }

        private readonly ISessionUsers _users;

        protected override void Execute(IUser user, SharedSessionLatency.PeerProbe context)
        {
            context.SenderIndex = user.Index;
            context.ServerTimestamp = Stopwatch.GetTimestamp();

            _users.SendAllExceptSelf(user, context);
        }
    }
}
