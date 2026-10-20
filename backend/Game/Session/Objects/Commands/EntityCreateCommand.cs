using Shared;

namespace Game.Session;

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
        // A client creates only in its own id range (SessionEntityIds): an id of another range is the id of another
        // user's entity, its player built by the session included.
        if (SessionEntityIds.IsInRangeOf(request.Id, user.Index) == false)
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

        _users.SendAllExceptSelf(user, entity.CreateOverview());

        return new SharedSessionEntity.CreateResponse()
        {
            EntityId = entity.Id
        };
    }
}
