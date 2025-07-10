using Shared;

namespace Game;

public class SetPropertyCommand : Command<ObjectContexts.SetProperty>
{
    public SetPropertyCommand(ISessionObjects objects, ISessionUsers users)
    {
        _objects = objects;
        _users = users;
    }
    
    private readonly ISessionObjects _objects;
    private readonly ISessionUsers _users;

    protected override void Execute(IUser user, ObjectContexts.SetProperty context)
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

        _users.SendAllExceptSelf(user, updatedContext);
    }
}