using Shared;

namespace Game;

public class EntityPropertyUpdateCommand : Command<EntityContexts.UpdatePropertyRequest>
{
    protected override void Execute(CommandScope scope, EntityContexts.UpdatePropertyRequest context)
    {
        var entities = scope.Session.Entities;

        var entity = entities.Entries[context.EntityId];
        var property = entity.Properties[context.PropertyId];
        property.Update(context.Value);

        var updatedContext = new EntityContexts.PropertyUpdate()
        {
            EntityId = context.EntityId,
            PropertyId = context.PropertyId,
            Value = context.Value
        };

        scope.SendAllExceptSelf(updatedContext);
    }
}