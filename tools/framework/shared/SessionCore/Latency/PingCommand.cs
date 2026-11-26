using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    public class PingCommand : Command<SharedSessionLatency.Ping>
    {
        protected override void Execute(IUser user, SharedSessionLatency.Ping context)
        {
            user.Send(new SharedSessionLatency.Pong()
            {
                Id = context.Id
            });
        }
    }
}
