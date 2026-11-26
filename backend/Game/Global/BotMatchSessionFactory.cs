using Cluster.Configs;
using Cluster.Discovery;
using Cluster.State;
using Common;
using Common.Extensions;
using Common.Network;
using Internal;
using Game.GamePlay;
using Game.MatchMetrics;
using Game.Session;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Global;
// A match of one player with a bot created in the session.
public class BotMatchSessionFactory
{
    public BotMatchSessionFactory(
        IServiceProvider serviceProvider,
        ISessionFactory sessionFactory,
        ISessionsCollection collection,
        ISessionTicketIssuer tickets,
        ILogger<SessionFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _sessionFactory = sessionFactory;
        _collection = collection;
        _tickets = tickets;
        _logger = logger;
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionFactory _sessionFactory;
    private readonly ISessionsCollection _collection;
    private readonly ISessionTicketIssuer _tickets;
    private readonly ILogger<SessionFactory> _logger;

    public Guid Create(Guid botId, MatchCreateOptions createOptions, Guid participant)
    {
        _logger.LogInformation("[Matchmaking] Creating match: {Type}", createOptions.Type);

        var lifetime = new Lifetime();

        var data = new SessionContainerData
        {
            Id = Guid.NewGuid(),
            Lifetime = lifetime,
            ExpectedUsers = 2,
        };

        // The bot and the human are expected; only the human holds a place (a bot never holds a session).
        var options = new SessionCreateOptions
        {
            Id = data.Id,
            Type = SessionType.Match,
            ExpectedUsers = 2,
            MaxUsers = 1,
            IsPrivate = true
        };

        var services = new ServiceCollection();

        services.PassSessionDefaults(_serviceProvider);

        services.AddSessionServices(data, options);
        services.AddCardServices();
        services.AddGameCommands();
        services.AddCheatCommands();
        services.AddGameContext();
        services.AddPlayerServices();
        services.AddBotServices();

        services.Add<BotGameReadyAwaiter>()
                .As<IGameReadyAwaiter>();

        services.Add<BotPlayersReadyAwaiter>()
                .As<IPlayersReadyAwaiter>();

        services.AddRound(createOptions.Type);

        services.Add<ISessionFactory>(_sessionFactory);
        services.Add<ISessionTicketIssuer>(_tickets);
        services.Add<MatchHandle>();
        services.Add(createOptions);

        var provider = services.BuildServiceProvider();
        var session = provider.GetRequiredService<ISession>();
        var serviceFactory = provider.GetRequiredService<IServiceFactory>();
        var userFactory = provider.GetRequiredService<IUserFactory>();
        var botRunner = provider.GetRequiredService<IBotRunner>();

        // The bot is created in the session itself (no socket, no ticket): only the human has a place to enter.
        if (session.Reserve(participant) != SessionReservation.Reserved)
            throw new InvalidOperationException($"A place of participant {participant} in match {data.Id} is refused");

        _collection.Add(session, createOptions.Type);

        RunSession().NoAwait();

        return data.Id;

        async Task RunSession()
        {
            await session.ExecutionQueue.SetupServices(serviceFactory);
            session.Run().NoAwait();
            lifetime.Listen(provider.Dispose);

            // Users are created in the execution queue only (the seats of the session live there).
            var botCreated = new TaskCompletionSource<IUser>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.ExecutionQueue.Enqueue(() => botCreated.SetResult(userFactory.CreateBot(session.Lifetime, botId)));
            var bot = await botCreated.Task;

            await session.AllUsersConnected;
            var handle = provider.GetRequiredService<MatchHandle>();

            _ = Task.Run(() => handle.Process());
            _ = Task.Run(() => botRunner.Run(bot));

            _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created",
                session.Id, createOptions);
        }
    }
}
