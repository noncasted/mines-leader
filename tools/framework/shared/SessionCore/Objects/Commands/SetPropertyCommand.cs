using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    public class SetPropertyCommand : Command<SharedSessionObject.SetProperty>
    {
        public SetPropertyCommand(ISessionObjects objects, ISessionUsers users, ISessionLog log)
        {
            _objects = objects;
            _users = users;
            _log = log;
        }

        private readonly ISessionObjects _objects;
        private readonly ISessionUsers _users;
        private readonly ISessionLog _log;

        protected override void Execute(IUser user, SharedSessionObject.SetProperty context)
        {
            if (_objects.Entries.TryGetValue(context.ObjectId, out var networkObject) == false)
            {
                // A write that crossed the destroy of the object (a game service destroys the session entities while
                // their owner still writes them): nobody holds the object any more.
                if (_objects.IsRemoved(context.ObjectId) == true)
                {
                    _log.Debug(
                        $"[Session] User {user.Index} writes property {context.PropertyId} of destroyed object {context.ObjectId}: dropped");

                    return;
                }

                // Never created in this session: a client bug, the command fails (CommandDispatcher logs it).
                throw new InvalidOperationException(
                    $"User {user.Index} writes property {context.PropertyId} of object {context.ObjectId} that does not exist in the session");
            }

            if (networkObject.Properties.TryGetValue(context.PropertyId, out var property) == false)
            {
                throw new InvalidOperationException(
                    $"User {user.Index} writes property {context.PropertyId} that object {context.ObjectId} does not have");
            }

            // Not the writer of the property (a guest of a lobby writing the start flag, a client writing what
            // only the backend writes): the write is dropped - not applied, not sent to the others. The session
            // has no error channel to a client, and a honest client never gets here: a warning is enough.
            if (property.CanBeWrittenBy(user) == false)
            {
                _log.Warning(
                    $"[Session] User {user.Index} is not the writer of property {context.PropertyId} of object {context.ObjectId}: the write is ignored");

                return;
            }

            property.Update(context.Value);

            var updatedContext = new SharedSessionObject.PropertyUpdate()
            {
                ObjectId = context.ObjectId,
                PropertyId = context.PropertyId,
                Value = context.Value,
                Version = property.Version
            };

            // A service property goes only to its subscribers: a client joining right now gets the value in its
            // GetResponse (the scheme written by the owner while another client connects).
            if (networkObject is IService service)
            {
                foreach (var subscriber in service.Subscribers)
                {
                    if (subscriber != user)
                        subscriber.Send(updatedContext);
                }

                return;
            }

            _users.SendAllExceptSelf(user, updatedContext);
        }
    }
}
