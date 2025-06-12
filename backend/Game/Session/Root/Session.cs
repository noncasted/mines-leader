using Common;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game;

public class Session : ISession
{
    public Session(
        ISessionMetadata metadata,
        IUserFactory userFactory,
        ISessionUsers users,
        ICommandsCollection commandsCollection,
        ISessionEntities entities,
        IEntityFactory entityFactory,
        ILifetime lifetime,
        IExecutionQueue executionQueue,
        ILogger<Session> logger,
        Guid id)
    {
        _commandsCollection = commandsCollection;
        _lifetime = lifetime;
        ExecutionQueue = executionQueue;
        _logger = logger;
        Metadata = metadata;
        UserFactory = userFactory;
        Users = users;
        Entities = entities;
        Id = id;
        EntityFactory = entityFactory;
    }

    private readonly ICommandsCollection _commandsCollection;
    private readonly ILifetime _lifetime;
    private readonly ILogger<Session> _logger;

    public Guid Id { get; }
    public IReadOnlyLifetime Lifetime => _lifetime;

    public ISessionMetadata Metadata { get; }
    public ISessionUsers Users { get; }
    public IUserFactory UserFactory { get; }
    public ISessionEntities Entities { get; }
    public IEntityFactory EntityFactory { get; }
    public IExecutionQueue ExecutionQueue { get; }

    public async Task Run()
    {
        await AwaitUsersJoin();

        Users.View(Lifetime, HandleUserJoin);

        await Task.Delay(TimeSpan.FromMinutes(3));

        await AwaitUsersLeave();

        _lifetime.Terminate();

        async Task AwaitUsersJoin()
        {
            if (Metadata.ExpectedUsers == 0)
                return;

            while (Users.Count < Metadata.ExpectedUsers)
                await Task.Delay(TimeSpan.FromSeconds(1));
        }

        async Task AwaitUsersLeave()
        {
            while (Users.Count != 0)
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

        Users.IterateOthers(user, other =>
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

        foreach (var (_, entity) in Entities.Entries)
            user.Send(entity.GetUpdateContext());

        var dispatcher = new CommandDispatcher(_commandsCollection, user, this, ExecutionQueue);
        dispatcher.Run(user.Lifetime).NoAwait();
        user.Reader.Run(user.Lifetime).NoAwait();

        user.Lifetime.Listen(() => HandleDisconnect(user));
    }

    private void HandleDisconnect(IUser sourceUser)
    {
        foreach (var user in Users)
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