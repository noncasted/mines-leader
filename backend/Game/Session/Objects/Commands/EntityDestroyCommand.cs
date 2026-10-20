using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

public class EntityDestroyCommand : Command<SharedSessionEntity.Destroy>
{
    public EntityDestroyCommand(
        ISessionEntities entities,
        ISessionObjects objects,
        ISessionUsers users,
        ILogger<EntityDestroyCommand> logger)
    {
        _entities = entities;
        _objects = objects;
        _users = users;
        _logger = logger;
    }

    private readonly ISessionEntities _entities;
    private readonly ISessionObjects _objects;
    private readonly ISessionUsers _users;
    private readonly ILogger<EntityDestroyCommand> _logger;

    protected override void Execute(IUser user, SharedSessionEntity.Destroy context)
    {
        if (_entities.Entries.TryGetValue(context.EntityId, out var entity) == false)
        {
            // Already gone here (destroyed with its owner that left): the other clients got their DestroyUpdate then.
            if (_objects.IsRemoved(context.EntityId) == true)
            {
                _logger.LogDebug("[Session] User {Index} destroys entity {EntityId} that is already destroyed: dropped",
                    user.Index,
                    context.EntityId);

                return;
            }

            // Never created in this session: a client bug, the command fails (the execution queue logs it).
            throw new InvalidOperationException(
                $"User {user.Index} destroys entity {context.EntityId} that does not exist in the session");
        }

        entity.Destroy();

        var update = new SharedSessionEntity.DestroyUpdate()
        {
            EntityId = context.EntityId
        };

        _users.SendAllExceptSelf(user, update);
    }
}
