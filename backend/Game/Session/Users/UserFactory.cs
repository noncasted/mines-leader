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
        var lifetime = parentLifetime.Child();

        var writer = new ConnectionWriter(webSocket, _logger);
        var reader = new ConnectionReader(webSocket);
        var dispatcher = new CommandDispatcher(_commandsCollection, _executionQueue);

        var user = new User
        {
            Id = userId,
            Index = index,
            Lifetime = lifetime,
            Reader = reader,
            Writer = writer,
            Dispatcher = dispatcher
        };

        _users.AddUser(user);

        _logger.LogInformation("[Session] User created: {Index} {UserId}", user.Index, user.Id);

        return user;
    }
}