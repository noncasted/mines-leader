using Common;
using Shared;

namespace Game;

public interface ISession
{
    Guid Id { get; }
    IReadOnlyLifetime Lifetime { get; }
    ISessionMetadata Metadata { get; }
    IUserFactory UserFactory { get; }
    IExecutionQueue ExecutionQueue { get; }

    Task Run(SessionContainerData data);
}

public class SessionCreateOptions
{
    public required int ExpectedUsers { get; init; }
    public required string Type { get; init; }
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
        ISessionMetadata metadata,
        IUserFactory userFactory,
        ISessionUsers users,
        ISessionEntities entities,
        IExecutionQueue executionQueue)
    {
        _users = users;
        _entities = entities;
        ExecutionQueue = executionQueue;
        Metadata = metadata;
        UserFactory = userFactory;
    }

    private readonly ISessionUsers _users;
    private readonly ISessionEntities _entities;
    
    private SessionContainerData _data;

    public Guid Id => _data.Id;
    public IReadOnlyLifetime Lifetime => _data.Lifetime;

    public ISessionMetadata Metadata { get; }
    public IUserFactory UserFactory { get; }
    public IExecutionQueue ExecutionQueue { get; }

    public async Task Run(SessionContainerData data)
    {
        _data = data;
        await AwaitUsersJoin();

        _users.View(Lifetime, HandleUserJoin);

        await Task.Delay(TimeSpan.FromMinutes(3));

        await AwaitUsersLeave();

        data.Lifetime.Terminate();

        async Task AwaitUsersJoin()
        {
            if (Metadata.ExpectedUsers == 0)
                return;

            while (_users.Count < Metadata.ExpectedUsers)
                await Task.Delay(TimeSpan.FromSeconds(1));
        }

        async Task AwaitUsersLeave()
        {
            while (_users.Count != 0)
                await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private void HandleUserJoin(IUser user)
    {
        user.Writer.Run(user.Lifetime).NoAwait();

        user.Send(new UserContexts.LocalUpdate()
        {
            Index = user.Index
        });

        _users.IterateOthers(user, other =>
        {
            user.Send(new UserContexts.RemoteUpdate()
            {
                Index = other.Index,
                BackendId = other.Id
            });

            other.Send(new UserContexts.RemoteUpdate()
            {
                Index = user.Index,
                BackendId = user.Id
            });
        });

        foreach (var (_, entity) in _entities.Entries)
            user.Send(entity.CreateOverview());

        user.Dispatcher.Run(user.Lifetime, user).NoAwait();
        user.Reader.Run(user.Lifetime).NoAwait();

        user.Lifetime.Listen(() => HandleDisconnect(user));
    }

    private void HandleDisconnect(IUser sourceUser)
    {
        foreach (var user in _users)
        {
            if (user == sourceUser)
                continue;

            user.Send(new UserContexts.RemoteDisconnect()
            {
                Index = sourceUser.Index
            });
        }
    }
}