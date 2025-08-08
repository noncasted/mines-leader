using Shared;

namespace Game;

public interface IPropertyUpdateSender
{
    void Send(int objectId, IObjectProperty property);
}

public class PropertyUpdateSender : IPropertyUpdateSender
{
    public PropertyUpdateSender(ISessionUsers users)
    {
        _users = users;
    }

    private readonly ISessionUsers _users;

    public void Send(int objectId, IObjectProperty property)
    {
        var context = new SharedSessionObject.PropertyUpdate()
        {
            ObjectId = objectId,
            PropertyId = property.Id,
            Value = property.RawValue
        };

        _users.SendAll(context);
    }
}