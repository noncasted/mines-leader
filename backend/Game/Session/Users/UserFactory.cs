using System.Net.WebSockets;
using Common;
using Microsoft.Extensions.Logging;

namespace Game;

public interface IUserFactory
{
    IUser Create(IReadOnlyLifetime lifetime, Guid userId, WebSocket webSocket);
}

public class UserFactory : IUserFactory
{
    public UserFactory(
        ISessionUsers users,
        IExecutionQueue executionQueue,
        ICommandsCollection commandsCollection,
        ILogger<UserFactory> logger)
    {
        _users = users;
        _logger = logger;
        _executionQueue = executionQueue;
        _commandsCollection = commandsCollection;
    }

    private readonly ISessionUsers _users;
    private readonly ILogger<UserFactory> _logger;
    private readonly IExecutionQueue _executionQueue;
    private readonly ICommandsCollection _commandsCollection;

    public IUser Create(IReadOnlyLifetime parentLifetime, Guid userId, WebSocket webSocket)
    {
        var index = _users.GetNextIndex();
        var connectionLifetime = parentLifetime.Child();
        var userLifetime = new Lifetime();
        connectionLifetime.Listen(() => _executionQueue.Enqueue(userLifetime.Terminate));

        var dispatcher = new CommandDispatcher(_commandsCollection, _executionQueue);
        var connection = new Connection(webSocket, parentLifetime, _logger);

        var user = new User
        {
            Id = userId,
            Index = index,
            Lifetime = userLifetime,
            Dispatcher = dispatcher,
            Connection = connection
        };

        _users.AddUser(user);

        _logger.LogInformation("[Session] User created: {Index} {UserId}", user.Index, user.Id);

        return user;
    }
}