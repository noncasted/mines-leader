using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    public class
        EntityCreateCommand : ResponseCommand<SharedSessionEntity.CreateRequest, SharedSessionEntity.CreateResponse>
    {
        public EntityCreateCommand(IEntityFactory entityFactory, ISessionObjects objects, ISessionUsers users)
        {
            _entityFactory = entityFactory;
            _objects = objects;
            _users = users;
        }

        private readonly IEntityFactory _entityFactory;
        private readonly ISessionObjects _objects;
        private readonly ISessionUsers _users;

        protected override SharedSessionEntity.CreateResponse Execute(IUser user, SharedSessionEntity.CreateRequest request)
        {
            // Every client creates in its own id range (SharedEntityIds), session entities too: an id outside it would
            // be taken by another user later.
            if (SharedEntityIds.IsInRangeOf(request.Id, user.Index) == false)
            {
                throw new SessionRequestException(Shared.ErrorCode.Forbidden,
                    $"User {user.Index} creates entity {request.Id} out of its id range");
            }

            // Ids are not reused: a live or a destroyed one is refused.
            if (_objects.Entries.ContainsKey(request.Id) == true || _objects.IsRemoved(request.Id) == true)
            {
                throw new SessionRequestException(Shared.ErrorCode.Forbidden,
                    $"User {user.Index} creates entity {request.Id} whose id is already used in the session");
            }

            var entity = _entityFactory.Create(request, user);

            // A returning client goes on after the last counter of its seat (LocalUpdate.EntityCounter).
            user.Seat.OnEntityCreated(entity.Id);

            _users.SendAllExceptSelf(user, entity.CreateOverview());

            return new SharedSessionEntity.CreateResponse()
            {
                EntityId = entity.Id
            };
        }
    }
}
