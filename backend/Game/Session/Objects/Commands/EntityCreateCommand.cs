using Shared;

namespace Game;

public class EntityCreateCommand : ResponseCommand<EntityContexts.CreateRequest, EntityContexts.CreateResponse>
{
    public EntityCreateCommand(IEntityFactory entityFactory, ISessionUsers users)
    {
        _entityFactory = entityFactory;
        _users = users;
    }
    
    private readonly IEntityFactory _entityFactory;
    private readonly ISessionUsers _users;

    protected override EntityContexts.CreateResponse Execute(IUser user, EntityContexts.CreateRequest context)
    {
        var entity = _entityFactory.Create(context, user);

        _users.SendAllExceptSelf(user, entity.CreateOverview());

        return new EntityContexts.CreateResponse()
        {
            EntityId = entity.Id
        };
    }
}