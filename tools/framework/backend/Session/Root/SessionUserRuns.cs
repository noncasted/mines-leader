using Microsoft.Extensions.Logging;

namespace Game.Session;

// The handling of one connected user: its commands and its socket run until the connection ends, then the user is
// removed.
internal class SessionUserRuns
{
    public SessionUserRuns(
        SessionContainerData data,
        ISessionPresence presence,
        IExecutionQueue executionQueue,
        ILogger logger)
    {
        _data = data;
        _presence = presence;
        _executionQueue = executionQueue;
        _logger = logger;
    }

    private readonly SessionContainerData _data;
    private readonly ISessionPresence _presence;
    private readonly IExecutionQueue _executionQueue;
    private readonly ILogger _logger;

    // The user is removed whatever its connection ends with: a run that fails (a connection terminated before it,
    // a transport error) is a disconnect too, otherwise the user would hold the session forever.
    public async Task Run(IUser user)
    {
        try
        {
            await Connect(user);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Session] Session {ID}: connection of user {Index} {UserId} has failed",
                _data.Id,
                user.Index,
                user.BackendId);
        }

        // What the others are told depends on the seat (SessionConnections): a leave, a loss or nothing.
        _executionQueue.Enqueue(user.Lifetime.Terminate);
    }

    private Task Connect(IUser user)
    {
        // The users of the backend come from its UserFactory only.
        var hosted = (IHostedUser)user;
        hosted.Dispatcher.Run(user.Lifetime, hosted);

        var connectionTask = hosted.Socket.Run();

        _presence.Introduce(user);

        return connectionTask;
    }
}
