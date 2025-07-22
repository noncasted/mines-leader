using Shared;

namespace Game;

public class EntityEventCommand : Command<SharedSessionObject.Event>
{
    public EntityEventCommand(ISessionUsers users)
    {
        _users = users;
    }

    private readonly ISessionUsers _users;

    protected override void Execute(IUser user, SharedSessionObject.Event context)
    {
        _users.SendAllExceptSelf(user, context);
    }
}