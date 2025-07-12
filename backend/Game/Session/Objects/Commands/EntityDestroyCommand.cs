using Shared;

namespace Game;

public class EntityDestroyCommand : Command<SharedSessionEntity.Destroy>
{
    public EntityDestroyCommand(ISessionEntities entities, ISessionUsers users)
    {
        _entities = entities;
        _users = users;
    }
    
    private readonly ISessionEntities _entities;
    private readonly ISessionUsers _users;

    protected override void Execute(IUser user, SharedSessionEntity.Destroy context)
    {
        var entity = _entities.Entries[context.EntityId];
        entity.Destroy();

        var update = new SharedSessionEntity.DestroyUpdate()
        {
            EntityId = context.EntityId
        };

        _users.SendAllExceptSelf(user, update);
    }
}