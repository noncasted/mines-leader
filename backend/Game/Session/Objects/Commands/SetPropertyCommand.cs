using Shared;

namespace Game;

public class SetPropertyCommand : Command<ObjectContexts.SetProperty>
{
    public SetPropertyCommand(ISessionObjects objects)
    {
        _objects = objects;
    }
    
    private readonly ISessionObjects _objects;

    protected override void Execute(CommandScope scope, ObjectContexts.SetProperty context)
    {
        var networkObject = _objects.Entries[context.ObjectId];
        var property = networkObject.Properties[context.PropertyId];
        property.Update(context.Value);

        var updatedContext = new ObjectContexts.PropertyUpdate()
        {
            ObjectId = context.ObjectId,
            PropertyId = context.PropertyId,
            Value = context.Value
        };

        scope.SendAllExceptSelf(updatedContext);
    }
}