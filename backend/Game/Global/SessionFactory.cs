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

public interface ISessionFactory
{
    Guid CreateLobby(LobbyCreateOptions createOptions);

    // S4: the places of the participants (meta ids of the humans) are reserved before the id is returned: a user
    // enters the session only with a ticket for it and a live place.
    Guid CreateMatch(MatchCreateOptions createOptions, IReadOnlyList<Guid> participants);
    Guid CreateMatchWithBot(Guid botId, MatchCreateOptions createOptions, Guid participant);
}

public class SessionFactory : ISessionFactory
{
    public SessionFactory(
        IServiceProvider serviceProvider,
        ISessionsCollection collection,
        ISessionTicketIssuer tickets,
        ILogger<SessionFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _collection = collection;
        _logger = logger;
        _matches = new MatchSessionFactory(serviceProvider, this, collection, tickets, logger);
        _botMatches = new BotMatchSessionFactory(serviceProvider, this, collection, tickets, logger);
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionsCollection _collection;
    private readonly ILogger<SessionFactory> _logger;
    private readonly MatchSessionFactory _matches;
    private readonly BotMatchSessionFactory _botMatches;

    public Guid CreateLobby(LobbyCreateOptions createOptions)
    {
        _logger.LogInformation("[Matchmaking] Creating lobby");

        var lifetime = new Lifetime();

        var data = new SessionContainerData
        {
            Id = Guid.NewGuid(),
            Lifetime = lifetime,
            ExpectedUsers = 0,
        };

        // Every user that searches gets into the first lobby; it closes when nobody holds it.
        var options = new SessionCreateOptions
        {
            Id = data.Id,
            Type = SessionType.Lobby,
            ExpectedUsers = 0,
            MaxUsers = int.MaxValue,
            IsPrivate = false
        };

        var services = new ServiceCollection();
        services.PassSessionDefaults(_serviceProvider);

        services.AddSessionServices(data, options);
        var provider = services.BuildServiceProvider();

        RunSession().NoAwait();

        return data.Id;

        async Task RunSession()
        {
            var session = provider.GetRequiredService<ISession>();
            var serviceFactory = provider.GetRequiredService<IServiceFactory>();

            _collection.Add(session);

            await session.ExecutionQueue.SetupServices(serviceFactory);
            session.Run().NoAwait();
            lifetime.Listen(provider.Dispose);

            _logger.LogInformation("[Matchmaking] Lobby {ID} created", session.Id);
        }
    }

    public Guid CreateMatch(MatchCreateOptions createOptions, IReadOnlyList<Guid> participants)
    {
        return _matches.Create(createOptions, participants);
    }

    public Guid CreateMatchWithBot(Guid botId, MatchCreateOptions createOptions, Guid participant)
    {
        return _botMatches.Create(botId, createOptions, participant);
    }
}
