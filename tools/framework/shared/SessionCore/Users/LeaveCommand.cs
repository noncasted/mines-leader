using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    // the client leaves the session on purpose and closes its connection right after this message: the end of
    // the connection is a leave (the seat and its entities end at once), not a loss with a reconnect window.
    public class LeaveCommand : Command<SharedSessionPlayer.Leave>
    {
        protected override void Execute(IUser user, SharedSessionPlayer.Leave context)
        {
            user.Seat.MarkLeaving(user);
        }
    }
}
