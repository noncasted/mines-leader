using Common.Extensions;
using Common.Reactive;
using Shared;

namespace Game.Session;

public interface ISession
{
    Guid Id { get; }
    SessionType Type { get; }
    IReadOnlyLifetime Lifetime { get; }
    IUserFactory UserFactory { get; }
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
}

public class SessionContainerData
{
    public required SessionType Type { get; init; }
    public required int ExpectedUsers { get; init; }
    public required Guid Id { get; init; }
    public required ILifetime Lifetime { get; init; }
}

public class Session : ISession
{
    public Session(
        SessionContainerData data,
        IUserFactory userFactory,
        ISessionUsers users,
        ISessionEntities entities,
        IExecutionQueue executionQueue)
    {
        _data = data;
        _users = users;
        _entities = entities;
        ExecutionQueue = executionQueue;
        UserFactory = userFactory;
    }

    private readonly ISessionUsers _users;
    private readonly ISessionEntities _entities;
    private readonly SessionContainerData _data;
    private readonly ViewableDelegate _allUsersConnected = new();

    public Guid Id => _data.Id;
    public SessionType Type => _data.Type;
    public IReadOnlyLifetime Lifetime => _data.Lifetime;

    public IUserFactory UserFactory { get; }
    public IExecutionQueue ExecutionQueue { get; }
    public IViewableDelegate AllUsersConnected => _allUsersConnected;

    public async Task Run()
    {
        await AwaitUsersJoin();

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

        await Task.Delay(TimeSpan.FromSeconds(30));

        await AwaitUsersLeave();

        _data.Lifetime.Terminate();

        async Task AwaitUsersJoin()
        {
            if (_data.ExpectedUsers == 0)
                return;

            while (_users.Count < _data.ExpectedUsers)
                await Task.Delay(TimeSpan.FromSeconds(1));
        }

        async Task AwaitUsersLeave()
        {
            while (_users.Count != 0)
                await Task.Delay(TimeSpan.FromSeconds(10));
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