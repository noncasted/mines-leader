using Shared;

namespace Game;

public class EntityDestroyCommand : Command<EntityContexts.Destroy>
{
    public EntityDestroyCommand(ISessionEntities entities)
    {
        _entities = entities;
    }
    
    private readonly ISessionEntities _entities;

    protected override void Execute(CommandScope scope, EntityContexts.Destroy context)
    {
        var entity = _entities.Entries[context.EntityId];
        entity.Destroy();

        var update = new EntityContexts.DestroyUpdate()
        {
            EntityId = context.EntityId
        };

        scope.SendAllExceptSelf(update);
    }
}