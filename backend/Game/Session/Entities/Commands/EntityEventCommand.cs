using Shared;

namespace Game;

public class EntityEventCommand : Command<EntityContexts.Event>
{
    protected override void Execute(CommandScope scope, EntityContexts.Event context)
    {
        scope.SendAllExceptSelf(context);
    }
}