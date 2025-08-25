using Shared;

namespace Game;

public class EntityCreateCommand : ResponseCommand<SharedSessionEntity.CreateRequest, SharedSessionEntity.CreateResponse>
{
    public EntityCreateCommand(IEntityFactory entityFactory, ISessionUsers users)
    {
        _entityFactory = entityFactory;
        _users = users;
    }
    
    private readonly IEntityFactory _entityFactory;
    private readonly ISessionUsers _users;

    protected override SharedSessionEntity.CreateResponse Execute(IUser user, SharedSessionEntity.CreateRequest request)
    {
        var entity = _entityFactory.Create(request, user);

        _users.SendAllExceptSelf(user, entity.CreateOverview());

        return new SharedSessionEntity.CreateResponse()
        {
            EntityId = entity.Id
        };
    }
}