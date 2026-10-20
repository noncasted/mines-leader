using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

public interface ISession
{
    Guid Id { get; }
    SessionType Type { get; }
    IReadOnlyLifetime Lifetime { get; }
    IUserFactory UserFactory { get; }
    ISessionUsers Users { get; }
    IExecutionQueue ExecutionQueue { get; }
    IViewableDelegate AllUsersConnected { get; }

    Task Run();
}

public class LobbyCreateOptions
{
}

public class MatchCreateOptions
{
    public required GameMatchType Type { get; init; }
    public AgentMatchFixture? Fixture { get; init; }
}

public class SessionContainerData
{
    public required SessionType Type { get; init; }
    public required int ExpectedUsers { get; init; }
    public required Guid Id { get; init; }
    public required ILifetime Lifetime { get; init; }

    // Match: all expected users must connect within this time, otherwise the session closes (a player that never
    // came must not hold it forever). Shorter than the keep-alive abort of a connected socket that is not read yet
    // (SessionConnectionMiddleware: interval + timeout).
    public TimeSpan JoinTimeout { get; init; } = TimeSpan.FromSeconds(30);

    // Lobby: the users found it by search and connect a moment later, so an empty lobby is kept this long
    // before it may close.
    public TimeSpan EmptyLobbyGrace { get; init; } = TimeSpan.FromSeconds(30);
}

public class Session : ISession
{
    public Session(
        SessionContainerData data,
        IUserFactory userFactory,
        ISessionUsers users,
        ISessionEntities entities,
        IExecutionQueue executionQueue,
        ILogger<Session> logger)
    {
        _data = data;
        _logger = logger;
        _users = users;
        _entities = entities;
        ExecutionQueue = executionQueue;
        UserFactory = userFactory;
    }

    private readonly ISessionUsers _users;
    private readonly ISessionEntities _entities;
    private readonly SessionContainerData _data;
    private readonly ILogger<Session> _logger;
    private readonly ViewableDelegate _allUsersConnected = new();

    public Guid Id => _data.Id;
    public SessionType Type => _data.Type;
    public IReadOnlyLifetime Lifetime => _data.Lifetime;

    public IUserFactory UserFactory { get; }
    public ISessionUsers Users => _users;
    public IExecutionQueue ExecutionQueue { get; }
    public IViewableDelegate AllUsersConnected => _allUsersConnected;

    public async Task Run()
    {
        if (await AwaitUsersJoin() == false)
        {
            // Ended before the start: the join timeout (logged there) or the session was closed from outside.
            _data.Lifetime.Terminate();
            return;
        }

        if (Type == SessionType.Lobby)
            await Task.Delay(_data.EmptyLobbyGrace);

        await AwaitPlayersLeave();

        _data.Lifetime.Terminate();
    }

    // Completes with true once the users are handled: at once without ExpectedUsers, otherwise when the last expected
    // one joins (no poll). False when the join timeout or the end of the session comes first. Users are added and
    // removed only in the execution queue: the wait, the timeout and the start are decided there too.
    private Task<bool> AwaitUsersJoin()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Lifetime.Listen(() => started.TrySetResult(false));

        ExecutionQueue.Enqueue(() => {
            if (Lifetime.IsTerminated == true)
                return;

            if (_data.ExpectedUsers == 0)
            {
                Start();
                return;
            }

            var waiting = Lifetime.Child();

            _users.View(waiting, _ => {
                if (_users.Count < _data.ExpectedUsers)
                    return;

                // Not in place: the listeners of this addition run on the current list first.
                ExecutionQueue.Enqueue(() => {
                    // Already started, or a user left in between: the next join checks again.
                    if (waiting.IsTerminated == true || _users.Count < _data.ExpectedUsers)
                        return;

                    waiting.Terminate();
                    Start();
                });
            });

            Task.Delay(_data.JoinTimeout).ContinueWith(_ => ExecutionQueue.Enqueue(() => {
                if (waiting.IsTerminated == true)
                    return;

                waiting.Terminate();

                _logger.LogWarning("[Session] Session {ID}: {Count} of {Expected} users joined in {Timeout} — closing",
                    Id,
                    _users.Count,
                    _data.ExpectedUsers,
                    _data.JoinTimeout);

                started.TrySetResult(false);
            }));
        });

        return started.Task;

        // Every user is handled once, including the ones that join after the start (a second connection of a
        // player): otherwise such a user is never run and its socket hangs.
        void Start()
        {
            _users.View(Lifetime, user => HandleUser(user).NoAwait());
            _allUsersConnected.Invoke();
            started.TrySetResult(true);
        }
    }

    // Completes as soon as no human user is left: a bot never leaves by itself (its connection lives as long as
    // the session), so a match with a bot would otherwise never close. A user leaves when its socket closes or
    // the keep-alive aborts a half-open one.
    private Task AwaitPlayersLeave()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Users are added and removed in the execution queue: the list is read there too.
        ExecutionQueue.Enqueue(() => {
            _users.View(Lifetime, (userLifetime, _) => userLifetime.Listen(Check));
            Check();
        });

        return completion.Task;

        void Check()
        {
            if (_users.All(user => user.IsBot == true))
                completion.TrySetResult();
        }
    }

    private async Task HandleUser(IUser user)
    {
        await HandleUserConnect(user);
        ExecutionQueue.Enqueue(() => HandleUserDisconnect(user));
    }

    private Task HandleUserConnect(IUser user)
    {
        user.Dispatcher.Run(user.Lifetime, user);

        var connectionTask = user.Connection.Run();

        user.Send(new SharedSessionPlayer.LocalUpdate()
        {
            Index = user.Index
        });

        _users.IterateOthers(user, other => {
            user.Send(new SharedSessionPlayer.RemoteUpdate()
            {
                Index = other.Index,
                BackendId = other.Id
            });

            other.Send(new SharedSessionPlayer.RemoteUpdate()
            {
                Index = user.Index,
                BackendId = user.Id
            });
        });

        foreach (var (_, entity) in _entities.Entries)
            user.Send(entity.CreateOverview());

        return connectionTask;
    }

    private void HandleUserDisconnect(IUser user)
    {
        foreach (var targetUser in _users)
        {
            if (targetUser == user)
                continue;

            targetUser.Send(new SharedSessionPlayer.RemoteDisconnect()
            {
                Index = user.Index
            });
        }

        ExecutionQueue.Enqueue(user.Lifetime.Terminate);
    }
}