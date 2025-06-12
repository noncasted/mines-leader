using Shared;

namespace Game;

public class EntityCreateCommand : ResponseCommand<EntityContexts.CreateRequest, EntityContexts.CreateResponse>
{
    public EntityCreateCommand(IEntityFactory entityFactory)
    {
        _entityFactory = entityFactory;
    }
    
    private readonly IEntityFactory _entityFactory;

    protected override EntityContexts.CreateResponse Execute(CommandScope scope, EntityContexts.CreateRequest context)
    {
        var entity = _entityFactory.Create(context, scope.User);

        scope.SendAllExceptSelf(entity.CreateOverview());

        return new EntityContexts.CreateResponse()
        {
            EntityId = entity.Id
        };
    }
}