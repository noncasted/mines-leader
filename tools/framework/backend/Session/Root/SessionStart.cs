using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;

namespace Game.Session;

// The moment a session starts handling its users: at once, or when the last of its expected users has joined. Those
// who have not all come in JoinTimeout end the session.
internal class SessionStart
{
    public SessionStart(
        SessionContainerData data,
        SessionCreateOptions options,
        ISessionUsers users,
        SessionUserRuns userRuns,
        SessionCloser closer,
        IExecutionQueue executionQueue,
        TimeProvider time,
        ILogger logger)
    {
        _data = data;
        _options = options;
        _users = users;
        _userRuns = userRuns;
        _closer = closer;
        _executionQueue = executionQueue;
        _time = time;
        _logger = logger;

        data.Lifetime.Listen(() => _allUsersConnected.TrySetCanceled());
    }

    private readonly SessionContainerData _data;
    private readonly SessionCreateOptions _options;
    private readonly ISessionUsers _users;
    private readonly SessionUserRuns _userRuns;
    private readonly SessionCloser _closer;
    private readonly IExecutionQueue _executionQueue;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource _allUsersConnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task AllUsersConnected => _allUsersConnected.Task;

    public Task Run()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _data.Lifetime.Listen(() => started.TrySetResult());

        // Users are added and removed only in the queue: the wait and the handling are set up there too.
        _executionQueue.Enqueue(() => AwaitUsersJoin(started));

        return started.Task;
    }

    // Runs in the execution queue. The expected users are awaited by the joins themselves, not by a poll: the last of
    // them starts the handling. A user that joins after it (a second connection) is handled too. Not all of them in
    // JoinTimeout - the session closes.
    private void AwaitUsersJoin(TaskCompletionSource started)
    {
        if (_data.Lifetime.IsTerminated == true)
            return;

        if (_data.ExpectedUsers == 0)
        {
            Start();
            return;
        }

        var waiting = _data.Lifetime.Child();

        _users.View(waiting, _ => {
            if (CountConnectedSeats() < _data.ExpectedUsers)
                return;

            // Not in place: the listeners of this addition run on the current list first.
            _executionQueue.Enqueue(() => {
                // Already started, or a user left in between: the next join checks again.
                if (waiting.IsTerminated == true || CountConnectedSeats() < _data.ExpectedUsers)
                    return;

                waiting.Terminate();
                Start();
            });
        });

        CloseIfNotJoined().NoAwait();

        async Task CloseIfNotJoined()
        {
            await Task.Delay(_options.JoinTimeout, _time, waiting.Token).ConfigureAwait(false);

            _executionQueue.Enqueue(() => {
                if (waiting.IsTerminated == true)
                    return;

                waiting.Terminate();

                _logger.LogWarning("[Session] Session {ID}: {Count} of {Expected} users joined in {Timeout}, closing",
                    _data.Id,
                    CountConnectedSeats(),
                    _data.ExpectedUsers,
                    _options.JoinTimeout);

                _closer.Abort();
            });
        }

        void Start()
        {
            _users.View(_data.Lifetime, user => _userRuns.Run(user).NoAwait());
            _allUsersConnected.TrySetResult();
            started.TrySetResult();
        }
    }

    // A user is expected once, whatever number of its connections are joined at the moment (a return before the start:
    // the old connection is dropped a moment later).
    private int CountConnectedSeats()
    {
        return _users.Select(user => user.Seat).Distinct().Count();
    }
}
