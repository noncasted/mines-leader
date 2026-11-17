using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    // The writes of one sync of a client (NetworkPropertiesCollector). Each entry is handled as its own SetProperty: a
    // failed one is logged like a failed one-way command, the others still apply.
    public class SetPropertiesCommand : Command<SharedSessionObject.SetProperties>
    {
        public SetPropertiesCommand(
            ISessionObjects objects,
            ISessionUsers users,
            ISessionLog log)
        {
            _single = new SetPropertyCommand(objects, users, log);
            _log = log;
        }

        private readonly ICommand _single;
        private readonly ISessionLog _log;

        protected override void Execute(IUser user, SharedSessionObject.SetProperties context)
        {
            foreach (var entry in context.Entries)
            {
                try
                {
                    _single.Execute(user, entry);
                }
                catch (Exception e)
                {
                    _log.Error(
                        $"[Session] [Command] Write of property {entry.PropertyId} of object {entry.ObjectId} by user {user.Index} failed",
                        e);
                }
            }
        }
    }
}
