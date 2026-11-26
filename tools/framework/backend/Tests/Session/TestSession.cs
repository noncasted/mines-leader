using Common.Network;
using Internal;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Framework.Tests.Session;

// One real session container (AddSessionServices) on a manual execution queue and manual time: no threads and no
// real delays. A user is created by the real UserFactory, over a TestConnection instead of a WebSocket;
// what the game gateway does (TryAccept, then the creation in the queue) is Enter. A connection ends through
// IConnection.Run, so a user leaves the session the way it does after a real disconnect (SessionUserRuns.Run).
public class TestSession
{
    // configure: what the host of a session of a game adds to its container (the services of the session, the
    // registrations of the lobbies module); it runs after AddSessionServices and before the queue override.
    public TestSession(SessionCreateOptions options, Action<IServiceCollection>? configure = null)
    {
        Options = options;
        Time = (ManualTimeProvider)options.Time;

        var data = new SessionContainerData
        {
            Id = options.Id,
            Lifetime = Lifetime,
            ExpectedUsers = options.ExpectedUsers
        };

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSessionServices(data, options);
        configure?.Invoke(services);

        // The last registration wins: the queue is pumped by the test.
        services.AddSingleton<IExecutionQueue>(Queue);

        Provider = services.BuildServiceProvider();

        Session = Provider.GetRequiredService<ISession>();
        Admission = Provider.GetRequiredService<ISessionAdmission>();
        Users = Provider.GetRequiredService<ISessionUsers>();
        Seats = Provider.GetRequiredService<ISessionSeats>();
        Entities = Provider.GetRequiredService<ISessionEntities>();
        Objects = Provider.GetRequiredService<ISessionObjects>();
        EntityFactory = Provider.GetRequiredService<IEntityFactory>();
        ServiceFactory = Provider.GetRequiredService<IServiceFactory>();
        Commands = Provider.GetRequiredService<ICommandsCollection>();

        // The owner follows the users from its creation: created before any user joins.
        Owner = Provider.GetRequiredService<ISessionOwner>();
    }

    public readonly Lifetime Lifetime = new();
    public readonly ManualExecutionQueue Queue = new();
    public readonly ManualTimeProvider Time;
    public readonly SessionCreateOptions Options;
    public readonly ServiceProvider Provider;
    public readonly ISession Session;
    public readonly ISessionAdmission Admission;
    public readonly ISessionUsers Users;
    public readonly ISessionSeats Seats;
    public readonly ISessionEntities Entities;
    public readonly ISessionObjects Objects;
    public readonly IEntityFactory EntityFactory;
    public readonly IServiceFactory ServiceFactory;
    public readonly ICommandsCollection Commands;
    public readonly ISessionOwner Owner;

    public Task RunTask { get; private set; } = Task.CompletedTask;

    public static TestSession Create(
        int expectedUsers = 0,
        int maxUsers = 4,
        SessionType type = SessionType.Match,
        TimeSpan? reconnectWindow = null,
        TimeSpan? joinTimeout = null,
        bool run = true)
    {
        var options = new SessionCreateOptions
        {
            Type = type,
            ExpectedUsers = expectedUsers,
            MaxUsers = maxUsers,
            IsPrivate = false,
            ReconnectWindow = reconnectWindow ?? TimeSpan.Zero,
            JoinTimeout = joinTimeout ?? TimeSpan.FromSeconds(30),
            Time = new ManualTimeProvider()
        };

        var session = new TestSession(options);

        if (run == true)
            session.Run();

        return session;
    }

    public Task Run()
    {
        RunTask = Session.Run();
        Queue.Drain();
        return RunTask;
    }

    // The search / the lobby: a place reserved for the user.
    public bool Reserve(Guid backendId)
    {
        return Session.Reserve(backendId) == SessionReservation.Reserved;
    }

    // The gateway: the place check, then the user is created in the queue. Null is the refusal.
    public IUser? Enter(Guid backendId)
    {
        if (Session.TryAccept(backendId) == false)
            return null;

        return Join(backendId);
    }

    // A reservation and the entry right after it: a new user of the session.
    public IUser ReserveAndEnter(Guid backendId)
    {
        if (Reserve(backendId) == false)
            throw new InvalidOperationException($"User {backendId} got no reservation");

        return Enter(backendId) ?? throw new InvalidOperationException($"User {backendId} was not accepted");
    }

    public IUser ReserveAndEnter()
    {
        return ReserveAndEnter(Guid.NewGuid());
    }

    // What the gateway does in the session queue after TryAccept: the real UserFactory.Create, over a TestConnection
    // instead of a WebSocket. connection: a connection built by the test (over Session.Lifetime), e.g. a failing one.
    public IUser Join(Guid backendId, IConnection? connection = null)
    {
        IUser? created = null;
        EnqueueJoin(backendId, user => created = user, connection);
        Queue.Drain();

        return created ?? throw new InvalidOperationException($"User {backendId} was not created");
    }

    // The creation of the user is only queued: the test decides what runs before it.
    public void EnqueueJoin(Guid backendId, Action<IUser>? onCreated = null, IConnection? connection = null)
    {
        Queue.Enqueue(() => {
            var user = Session.UserFactory.Create(Session.Lifetime, backendId, connection ?? new TestConnection(Session.Lifetime));
            onCreated?.Invoke(user);
        });
    }

    // A bot of the session (the real UserFactory.CreateBot), connected until the session ends.
    public IUser AddBot()
    {
        IUser? created = null;
        Queue.Enqueue(() => created = Session.UserFactory.CreateBot(Session.Lifetime, Guid.NewGuid()));
        Queue.Drain();

        return created ?? throw new InvalidOperationException("The bot was not created");
    }

    // The client leaves on purpose: SharedSessionPlayer.Leave, then the socket closes.
    public void Leave(IUser user)
    {
        var connection = Connection(user);
        connection.ReceiveOneWay(new SharedSessionPlayer.Leave());
        connection.Close();
        Queue.Drain();

        if (user.Lifetime.IsTerminated == false)
            throw new InvalidOperationException($"User {user.Index} has not left: is the session handling its users?");
    }

    // The socket is lost without a leave.
    public void Lose(IUser user)
    {
        Connection(user).Close();
        Queue.Drain();

        if (user.Lifetime.IsTerminated == false)
            throw new InvalidOperationException($"User {user.Index} is still connected: is the session handling its users?");
    }

    public void Advance(TimeSpan delta)
    {
        Time.Advance(delta);
        Queue.Drain();
    }

    // A one-way command of the client, executed directly (a failure throws to the test instead of being logged).
    public void Execute(IUser user, INetworkContext context)
    {
        Commands.OneWay[context.GetType()].Execute(user, context);
    }

    public INetworkContext Request(IUser user, INetworkContext context)
    {
        return Commands.Responsible[context.GetType()].Execute(user, context);
    }

    public static TestConnection Connection(IUser user)
    {
        return (TestConnection)((IHostedUser)user).Socket;
    }

    public static IReadOnlyList<INetworkContext> Sent(IUser user)
    {
        return Connection(user).Sent;
    }

    public static IReadOnlyList<T> Sent<T>(IUser user)
    {
        return Connection(user).Sent.OfType<T>().ToList();
    }

    public void ClearSent()
    {
        foreach (var user in Users)
        {
            if (user.IsBot == false)
                Connection(user).ClearSent();
        }
    }
}
