using Shared;

namespace Game;

public class EntityEventCommand : Command<ObjectContexts.Event>
{
    public EntityEventCommand(ISessionUsers users)
    {
        _users = users;
    }

    private readonly ISessionUsers _users;

    protected override void Execute(IUser user, ObjectContexts.Event context)
    {
        _users.SendAllExceptSelf(user, context);
    }
}