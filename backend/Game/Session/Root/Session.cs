using Common;
using Shared;

namespace Game;

public interface ISession
{
    Guid Id { get; }
    IReadOnlyLifetime Lifetime { get; }
    IUserFactory UserFactory { get; }
    IExecutionQueue ExecutionQueue { get; }
    SessionCreateOptions CreateOptions { get; }

    Task Run();
}

public class SessionCreateOptions
{
    public required int ExpectedUsers { get; init; }
    public required SessionType Type { get; init; }
}

public class SessionContainerData
{
    public required SessionCreateOptions CreateOptions { get; init; }
    public required Guid Id { get; init; }
    public required ILifetime Lifetime { get; init; }
}

public class Session : ISession
{
    public Session(
        SessionContainerData data,
        ISessionEvents events,
        IUserFactory userFactory,
        ISessionUsers users,
        ISessionEntities entities,
        IExecutionQueue executionQueue)
    {
        _data = data;
        _events = events;
        _users = users;
        _entities = entities;
        ExecutionQueue = executionQueue;
        UserFactory = userFactory;
    }

    private readonly ISessionUsers _users;
    private readonly ISessionEntities _entities;
    private readonly SessionContainerData _data;
    private readonly ISessionEvents _events;

    public Guid Id => _data.Id;
    public IReadOnlyLifetime Lifetime => _data.Lifetime;

    public IUserFactory UserFactory { get; }
    public IExecutionQueue ExecutionQueue { get; }
    public SessionCreateOptions CreateOptions => _data.CreateOptions;

    public async Task Run()
    {
        await _events.OnCreated(Lifetime);
        await AwaitUsersJoin();

        if (_data.CreateOptions.ExpectedUsers == 0)
        {
            _users.View(Lifetime, user => HandleUserConnect(user).NoAwait());
        }
        else
        {
            foreach (var user in _users)
            {
                var connectionTask = await HandleUserConnect(user);
                HandleUserDisconnect(user, connectionTask).NoAwait();
            }
        }

        await _events.OnAllConnected(Lifetime);

        await Task.Delay(TimeSpan.FromSeconds(30));

        await AwaitUsersLeave();

        _data.Lifetime.Terminate();

        async Task AwaitUsersJoin()
        {
            if (_data.CreateOptions.ExpectedUsers == 0)
                return;

            while (_users.Count < _data.CreateOptions.ExpectedUsers)
                await Task.Delay(TimeSpan.FromSeconds(1));
        }

        async Task AwaitUsersLeave()
        {
            while (_users.Count != 0)
                await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private async Task<Task> HandleUserConnect(IUser user)
    {
        user.Dispatcher.Run(user.Lifetime, user);

        var connectionTask = user.Connection.Run();

        user.Send(new SharedSessionPlayer.LocalUpdate()
            {
                Index = user.Index
            }
        );

        _users.IterateOthers(user, other =>
            {
                user.Send(new SharedSessionPlayer.RemoteUpdate()
                    {
                        Index = other.Index,
                        BackendId = other.Id
                    }
                );

                other.Send(new SharedSessionPlayer.RemoteUpdate()
                    {
                        Index = user.Index,
                        BackendId = user.Id
                    }
                );
            }
        );

        foreach (var (_, entity) in _entities.Entries)
            user.Send(entity.CreateOverview());

        return connectionTask;
    }

    private async Task HandleUserDisconnect(IUser user, Task connectionTask)
    {
        await connectionTask;

        foreach (var targetUser in _users)
        {
            if (targetUser == user)
                continue;

            targetUser.Send(new SharedSessionPlayer.RemoteDisconnect()
                {
                    Index = user.Index
                }
            );
        }

        ExecutionQueue.Enqueue(user.Lifetime.Terminate);
    }
}