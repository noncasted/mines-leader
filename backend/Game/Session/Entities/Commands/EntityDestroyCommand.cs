using Shared;

namespace Game;

public class EntityDestroyCommand : Command<EntityContexts.Destroy>
{
    protected override void Execute(CommandScope scope, EntityContexts.Destroy context)
    {
        var entity = scope.Session.Entities.Entries[context.EntityId];
        entity.Destroy();

        var update = new EntityContexts.DestroyUpdate()
        {
            EntityId = context.EntityId
        };

        scope.SendAllExceptSelf(update);
    }
}