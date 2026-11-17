using System.Net.WebSockets;
using Common.Network;
using Internal;
using Microsoft.Extensions.Logging;

namespace Game.Session;

public interface IUserFactory
{
    // backendId: the meta id of the user (the one of its ticket); the user gets its id in this session (IUser.Id).
    IUser Create(IReadOnlyLifetime lifetime, Guid backendId, WebSocket webSocket);

    // A user over a connection built by the caller over the same lifetime (a transport other than a WebSocket, the
    // tests).
    IUser Create(IReadOnlyLifetime lifetime, Guid backendId, IConnection connection);

    // A user played by the backend (IUser.IsBot), connected until the lifetime ends.
    IUser CreateBot(IReadOnlyLifetime lifetime, Guid backendId);
}

public class UserFactory : IUserFactory
{
    public UserFactory(
        ISessionUsers users,
        ISessionSeats seats,
        IExecutionQueue executionQueue,
        ISessionCommandRunner runner,
        ILogger<UserFactory> logger)
    {
        _users = users;
        _seats = seats;
        _logger = logger;
        _executionQueue = executionQueue;
        _runner = runner;
    }

    private readonly ISessionUsers _users;
    private readonly ISessionSeats _seats;
    private readonly ILogger<UserFactory> _logger;
    private readonly IExecutionQueue _executionQueue;
    private readonly ISessionCommandRunner _runner;

    public IUser Create(IReadOnlyLifetime parentLifetime, Guid backendId, WebSocket webSocket)
    {
        return Create(parentLifetime, backendId, new Connection(webSocket, parentLifetime, _logger));
    }

    public IUser Create(IReadOnlyLifetime parentLifetime, Guid backendId, IConnection connection)
    {
        var user = Create(parentLifetime, backendId, connection, false);

        _logger.LogInformation("[Session] User created: {Index} {UserId} {SessionUserId}, return: {IsReturn}",
            user.Index,
            user.BackendId,
            user.Id,
            user.Seat.IsReturn);

        return user;
    }

    public IUser CreateBot(IReadOnlyLifetime parentLifetime, Guid backendId)
    {
        var connection = new BotConnection
        {
            Lifetime = parentLifetime
        };

        var user = Create(parentLifetime, backendId, connection, true);

        _logger.LogInformation("[Session] Bot created: {Index} {UserId} {SessionUserId}", user.Index, user.BackendId, user.Id);

        return user;
    }

    private IUser Create(IReadOnlyLifetime parentLifetime, Guid backendId, IConnection connection, bool isBot)
    {
        // The seat of the user if it is still in the session (a reconnect: the same index), otherwise a new one.
        var seat = _seats.Take(backendId);
        var userLifetime = new Lifetime();

        // The user is removed in the queue once its connection ends, however it ends: closed by the client, forced by
        // the session (also before the session runs it) or with the session. The connection is bounded by the
        // session; a child ended with the user leaves nothing on the session lifetime (a seat may have many
        // connections over a match).
        var connectionLifetime = connection.Lifetime.Intersect(parentLifetime);
        connectionLifetime.Listen(() => _executionQueue.Enqueue(userLifetime.Terminate));
        userLifetime.Listen(connectionLifetime.Terminate);

        var dispatcher = new CommandDispatcher(_runner, _executionQueue);
        var user = new HostedUser(seat, isBot, userLifetime, connection, dispatcher);

        _seats.Attach(user);
        _users.AddUser(user);

        return user;
    }
}
