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
            _logger.LogWarning("[Session] Session {ID}: {Count} of {Expected} users joined in {Timeout} — closing",
                Id,
                _users.Count,
                _data.ExpectedUsers,
                _data.JoinTimeout);

            _data.Lifetime.Terminate();
            return;
        }

        if (_data.ExpectedUsers == 0)
        {
            _users.View(Lifetime, user => HandleUser(user).NoAwait());
        }
        else
        {
            foreach (var user in _users)
                HandleUser(user).NoAwait();
        }

        _allUsersConnected.Invoke();

        if (Type == SessionType.Lobby)
            await Task.Delay(_data.EmptyLobbyGrace);

        await AwaitPlayersLeave();

        _data.Lifetime.Terminate();

        async Task<bool> AwaitUsersJoin()
        {
            if (_data.ExpectedUsers == 0)
                return true;

            var deadline = DateTime.UtcNow + _data.JoinTimeout;

            while (_users.Count < _data.ExpectedUsers)
            {
                if (DateTime.UtcNow >= deadline)
                    return false;

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            return true;
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