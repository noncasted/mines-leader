using Shared;

namespace Game;

public class EntityCreateCommand : ResponseCommand<EntityContexts.CreateRequest, EntityContexts.CreateResponse>
{
    protected override EntityContexts.CreateResponse Execute(CommandScope scope, EntityContexts.CreateRequest context)
    {
        var entity = scope.Session.EntityFactory.Create(context, scope.User);

        scope.SendAllExceptSelf(entity.GetUpdateContext());

        return new EntityContexts.CreateResponse()
        {
            EntityId = entity.Id
        };
    }
}