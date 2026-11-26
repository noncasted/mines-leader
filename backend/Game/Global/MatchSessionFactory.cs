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
// A match of the players found by the search: every participant holds a place.
public class MatchSessionFactory
{
    public MatchSessionFactory(
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

    public Guid Create(MatchCreateOptions createOptions, IReadOnlyList<Guid> participants)
    {
        if (participants.Count == 0 || participants.Distinct().Count() != participants.Count)
            throw new ArgumentException($"A match needs distinct participants, got {participants.Count}", nameof(participants));

        _logger.LogInformation("[Matchmaking] Creating match: {Type}", createOptions.Type);

        var lifetime = new Lifetime();

        var data = new SessionContainerData
        {
            Id = Guid.NewGuid(),
            Lifetime = lifetime,
            ExpectedUsers = 2,
        };

        // No reconnect: a player whose connection is lost has left the match.
        var options = new SessionCreateOptions
        {
            Id = data.Id,
            Type = SessionType.Match,
            ExpectedUsers = 2,
            MaxUsers = participants.Count,
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

        services.Add<GameReadyAwaiter>()
                .As<IGameReadyAwaiter>();

        services.Add<PlayersReadyAwaiter>()
                .As<IPlayersReadyAwaiter>();

        services.AddRound(createOptions.Type);

        services.Add<ISessionFactory>(_sessionFactory);
        services.Add<ISessionTicketIssuer>(_tickets);
        services.Add<MatchHandle>();
        services.Add(createOptions);

        var provider = services.BuildServiceProvider();
        var session = provider.GetRequiredService<ISession>();
        var serviceFactory = provider.GetRequiredService<IServiceFactory>();

        foreach (var participant in participants)
        {
            if (session.Reserve(participant) != SessionReservation.Reserved)
                throw new InvalidOperationException($"A place of participant {participant} in match {data.Id} is refused");
        }

        _collection.Add(session, createOptions.Type);

        RunSession().NoAwait();

        return data.Id;

        async Task RunSession()
        {
            await session.ExecutionQueue.SetupServices(serviceFactory);
            session.Run().NoAwait();
            lifetime.Listen(provider.Dispose);

            await session.AllUsersConnected;
            var handle = provider.GetRequiredService<MatchHandle>();
            handle.Process().NoAwait();

            _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created",
                session.Id, createOptions);
        }
    }
}
