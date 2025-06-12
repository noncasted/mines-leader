using Shared;

namespace Game;

public class SetPropertyCommand : Command<ObjectContexts.SetProperty>
{
    public SetPropertyCommand(ISessionProperties properties)
    {
        _properties = properties;
    }
    
    private readonly ISessionProperties _properties;

    protected override void Execute(CommandScope scope, ObjectContexts.SetProperty context)
    {
        var property = _properties.Entries[context.PropertyId];
        property.Update(context.Value);

        var updatedContext = new ObjectContexts.PropertyUpdate()
        {
            Id = context.PropertyId,
            Value = context.Value
        };

        scope.SendAllExceptSelf(updatedContext);
    }
}