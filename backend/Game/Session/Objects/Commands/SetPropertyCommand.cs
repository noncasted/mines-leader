using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

public class SetPropertyCommand : Command<SharedSessionObject.SetProperty>
{
    public SetPropertyCommand(ISessionObjects objects, ISessionUsers users, ILogger<SetPropertyCommand> logger)
    {
        _objects = objects;
        _users = users;
        _logger = logger;
    }

    private readonly ISessionObjects _objects;
    private readonly ISessionUsers _users;
    private readonly ILogger<SetPropertyCommand> _logger;

    protected override void Execute(IUser user, SharedSessionObject.SetProperty context)
    {
        if (_objects.Entries.TryGetValue(context.ObjectId, out var networkObject) == false)
        {
            // A write that crossed the destroy of the object (destroyed by another command or with its owner that
            // left): nobody holds the object any more.
            if (_objects.IsRemoved(context.ObjectId) == true)
            {
                _logger.LogDebug(
                    "[Session] User {Index} writes property {PropertyId} of destroyed object {ObjectId}: dropped",
                    user.Index,
                    context.PropertyId,
                    context.ObjectId);

                return;
            }

            // Never created in this session: a client bug, the command fails (the execution queue logs it).
            throw new InvalidOperationException(
                $"User {user.Index} writes property {context.PropertyId} of object {context.ObjectId} that does not exist in the session");
        }

        if (networkObject.Properties.TryGetValue(context.PropertyId, out var property) == false)
        {
            throw new InvalidOperationException(
                $"User {user.Index} writes property {context.PropertyId} that object {context.ObjectId} does not have");
        }

        property.Update(context.Value);

        var updatedContext = new SharedSessionObject.PropertyUpdate()
        {
            ObjectId = context.ObjectId,
            PropertyId = context.PropertyId,
            Value = context.Value,
            Version = property.Version
        };

        _users.SendAllExceptSelf(user, updatedContext);
    }
}
