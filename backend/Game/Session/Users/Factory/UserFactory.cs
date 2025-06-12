using System.Net.WebSockets;
using Common;
using Microsoft.Extensions.Logging;

namespace Game;

public class UserFactory : IUserFactory
{
    public UserFactory(ISessionUsers users, ILogger logger, IExecutionQueue executionQueue)
    {
        _users = users;
        _logger = logger;
        _executionQueue = executionQueue;
    }

    private readonly ISessionUsers _users;
    private readonly ILogger _logger;
    private readonly IExecutionQueue _executionQueue;

    public IUser Create(IReadOnlyLifetime parentLifetime, Guid userId, WebSocket webSocket)
    {
        var index = _users.GetNextIndex();
        var lifetime = parentLifetime.Child();

        var writer = new WebSocketWriter(webSocket, _logger);
        var reader = new WebSocketReader(webSocket, _executionQueue);

        var user = new User
        {
            Id = userId,
            Index = index,
            Lifetime = lifetime,
            Reader = reader,
            Writer = writer
        };
        
        _users.AddUser(user);
        
        _logger.LogInformation("[Session] User created: {Index} {UserId}", user.Index, user.Id);

        return user;
    }
}