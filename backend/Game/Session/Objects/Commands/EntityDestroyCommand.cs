using Shared;

namespace Game;

public class EntityDestroyCommand : Command<EntityContexts.Destroy>
{
    public EntityDestroyCommand(ISessionEntities entities, ISessionUsers users)
    {
        _entities = entities;
        _users = users;
    }
    
    private readonly ISessionEntities _entities;
    private readonly ISessionUsers _users;

    protected override void Execute(IUser user, EntityContexts.Destroy context)
    {
        var entity = _entities.Entries[context.EntityId];
        entity.Destroy();

        var update = new EntityContexts.DestroyUpdate()
        {
            EntityId = context.EntityId
        };

        _users.SendAllExceptSelf(user, update);
    }
}