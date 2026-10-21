using Common.Reactive;
using Game.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Tests.Game;

// A real session of the framework without the game services, for the tests of the gateway of this game (the session
// itself is covered by the framework tests).
internal sealed class SessionWorld
{
    public SessionWorld(SessionType type, int expectedUsers, TimeProvider? time = null, TimeSpan? joinTimeout = null)
    {
        Data = new SessionContainerData
        {
            Id = Guid.NewGuid(),
            ExpectedUsers = expectedUsers,
            Lifetime = new Lifetime()
        };

        var options = new SessionCreateOptions
        {
            Id = Data.Id,
            Type = type,
            ExpectedUsers = expectedUsers,
            MaxUsers = type == SessionType.Lobby ? int.MaxValue : expectedUsers,
            IsPrivate = type == SessionType.Match,
            JoinTimeout = joinTimeout ?? TimeSpan.FromMilliseconds(300),
            Time = time ?? TimeProvider.System
        };

        var sessionData = new SessionData
        {
            Type = type,
            Id = Data.Id,
            ExpectedUsers = expectedUsers,
            Lifetime = Data.Lifetime
        };

        var queue = new ExecutionQueue(NullLogger<ExecutionQueue>.Instance);
        var users = new SessionUsers();
        _queue = queue;
        _users = users;
        var seats = new SessionSeats(users, sessionData);
        var commands = new CommandsCollection([], []);

        var userFactory = new UserFactory(users,
            seats,
            queue,
            commands,
            NullLogger<UserFactory>.Instance,
            NullLogger<CommandDispatcher>.Instance);

        Session = new global::Game.Session.Session(Data,
            options,
            userFactory,
            users,
            seats,
            new SessionEntities(),
            queue,
            new SessionAdmission(options),
            options.Time,
            NullLogger<global::Game.Session.Session>.Instance);
    }

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly ExecutionQueue _queue;
    private readonly SessionUsers _users;

    public SessionContainerData Data { get; }
    public ISession Session { get; }

    // The users of the session, read in its queue.
    public async Task<List<IUser>> GetUsers()
    {
        var completion = new TaskCompletionSource<List<IUser>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(() => completion.SetResult(_users.ToList()));
        return await completion.Task.WaitAsync(Wait);
    }

    public async Task WaitUsersCount(int count)
    {
        var deadline = DateTime.UtcNow + Wait;

        while (true)
        {
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(() => completion.SetResult(_users.Count));

            if (await completion.Task.WaitAsync(Wait) == count)
                return;

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"Users count did not reach {count}");

            await Task.Delay(20);
        }
    }
}
