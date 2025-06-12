using Shared;

namespace Game;

public class ServiceEntityCreateCommand :
    ResponseCommand<EntityContexts.GetServiceRequest,
        EntityContexts.GetServiceResponse>
{
    protected override EntityContexts.GetServiceResponse Execute(
        CommandScope scope,
        EntityContexts.GetServiceRequest context)
    {
        var entity = scope.Session.EntityFactory.GetOrCreateService(context);

        return new EntityContexts.GetServiceResponse()
        {
            EntityId = entity.Id,
            Properties = entity.Properties.Select(x => x.Value).ToList(),
        };
    }
}