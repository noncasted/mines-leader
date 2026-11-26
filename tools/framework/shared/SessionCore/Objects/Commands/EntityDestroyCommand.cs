using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    public class EntityDestroyCommand : Command<SharedSessionEntity.Destroy>
    {
        public EntityDestroyCommand(
            ISessionEntities entities,
            ISessionObjects objects,
            ISessionUsers users,
            ISessionLog log)
        {
            _entities = entities;
            _objects = objects;
            _users = users;
            _log = log;
        }

        private readonly ISessionEntities _entities;
        private readonly ISessionObjects _objects;
        private readonly ISessionUsers _users;
        private readonly ISessionLog _log;

        protected override void Execute(IUser user, SharedSessionEntity.Destroy context)
        {
            if (_entities.Entries.TryGetValue(context.EntityId, out var entity) == false)
            {
                // Already gone here (a game service destroyed the session entities, the creator left): the other
                // clients got their DestroyUpdate then.
                if (_objects.IsRemoved(context.EntityId) == true)
                {
                    _log.Debug($"[Session] User {user.Index} destroys entity {context.EntityId} that is already destroyed: dropped");

                    return;
                }

                // Never created in this session: a client bug, the command fails (CommandDispatcher logs it).
                throw new InvalidOperationException(
                    $"User {user.Index} destroys entity {context.EntityId} that does not exist in the session");
            }

            entity.Destroy();

            var update = new SharedSessionEntity.DestroyUpdate()
            {
                EntityId = context.EntityId
            };

            _users.SendAllExceptSelf(user, update);
        }
    }
}
