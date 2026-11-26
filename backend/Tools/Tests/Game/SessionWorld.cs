using Internal;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSessionServices(Data, options);
        var provider = services.BuildServiceProvider();
        Data.Lifetime.Listen(provider.Dispose);

        _queue = provider.GetRequiredService<IExecutionQueue>();
        _users = provider.GetRequiredService<ISessionUsers>();
        Session = provider.GetRequiredService<ISession>();
    }

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly IExecutionQueue _queue;
    private readonly ISessionUsers _users;

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
