using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Shared;

namespace Game.Session
{
    public class PeerEchoCommand : Command<SharedSessionLatency.PeerEcho>
    {
        public PeerEchoCommand(ISessionUsers users)
        {
            _users = users;
        }

        private readonly ISessionUsers _users;

        protected override void Execute(IUser user, SharedSessionLatency.PeerEcho context)
        {
            context.ResponderIndex = user.Index;
            context.ResponderLegMs = (float)((Stopwatch.GetTimestamp() - context.ServerTimestamp) * 1000.0 / Stopwatch.Frequency);

            // The sender may have left while the probe was on its way: the echo has nobody to go to.
            foreach (var target in _users)
            {
                if (target.Index == context.SenderIndex)
                {
                    target.Send(context);
                    return;
                }
            }
        }
    }
}
