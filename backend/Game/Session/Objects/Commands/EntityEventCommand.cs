using Shared;

namespace Game;

public class EntityEventCommand : Command<ObjectContexts.Event>
{
    protected override void Execute(CommandScope scope, ObjectContexts.Event context)
    {
        scope.SendAllExceptSelf(context);
    }
}