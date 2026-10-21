using System.Collections.Concurrent;
using System.Net.WebSockets;
using Common.Network;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Game.Session;

public interface IUserFactory
{
    // backendId: the meta id of the user (the one of its ticket); the user gets its id in this session (IUser.Id).
    IUser Create(IReadOnlyLifetime lifetime, Guid backendId, WebSocket webSocket);
    IUser CreateBot(IReadOnlyLifetime lifetime, Guid backendId);
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

    // The session id of a meta user is the same for every connection of it in this session.
    private readonly ConcurrentDictionary<Guid, Guid> _sessionIds = new();

    public IUser Create(IReadOnlyLifetime parentLifetime, Guid backendId, WebSocket webSocket)
    {
        var index = _users.GetNextIndex();
        var connectionLifetime = parentLifetime.Child();
        var userLifetime = new Lifetime();
        connectionLifetime.Listen(() => _executionQueue.Enqueue(userLifetime.Terminate));

        var dispatcher = new CommandDispatcher(_commandsCollection, _executionQueue, _logger);
        var connection = new Connection(webSocket, parentLifetime, _logger);

        var user = new User
        {
            Id = GetSessionId(backendId),
            BackendId = backendId,
            Index = index,
            IsBot = false,
            Lifetime = userLifetime,
            Dispatcher = dispatcher,
            Connection = connection
        };

        _users.AddUser(user);

        _logger.LogInformation("[Session] User created: {Index} {UserId} {SessionUserId}", user.Index, user.BackendId, user.Id);

        return user;
    }

    public IUser CreateBot(IReadOnlyLifetime parentLifetime, Guid backendId)
    {
        var index = _users.GetNextIndex();
        var connectionLifetime = parentLifetime.Child();
        var userLifetime = new Lifetime();
        connectionLifetime.Listen(() => _executionQueue.Enqueue(userLifetime.Terminate));

        var dispatcher = new CommandDispatcher(_commandsCollection, _executionQueue, _logger);

        var connection = new BotConnection
        {
            Lifetime = parentLifetime
        };

        var user = new User
        {
            Id = GetSessionId(backendId),
            BackendId = backendId,
            Index = index,
            IsBot = true,
            Lifetime = userLifetime,
            Dispatcher = dispatcher,
            Connection = connection
        };

        _users.AddUser(user);

        _logger.LogInformation("[Session] Bot created: {Index} {UserId} {SessionUserId}", user.Index, user.BackendId, user.Id);

        return user;
    }

    private Guid GetSessionId(Guid backendId)
    {
        return _sessionIds.GetOrAdd(backendId, _ => Guid.NewGuid());
    }
}