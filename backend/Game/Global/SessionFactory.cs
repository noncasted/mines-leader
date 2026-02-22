using Cluster.Configs;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Game.GamePlay;
using Game.Session;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Global;

public interface ISessionFactory
{
    Guid CreateLobby(LobbyCreateOptions createOptions);
    Guid CreateMatch(MatchCreateOptions createOptions);
}

public class SessionFactory : ISessionFactory
{
    public SessionFactory(
        IServiceProvider serviceProvider,
        ISessionsCollection collection,
        ILogger<SessionFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _collection = collection;
        _logger = logger;
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionsCollection _collection;
    private readonly ILogger<SessionFactory> _logger;

    public Guid CreateLobby(LobbyCreateOptions createOptions)
    {
        _logger.LogInformation("[Matchmaking] Creating lobby");

        var lifetime = new Lifetime();

        var data = new SessionContainerData
        {
            Id = Guid.NewGuid(),
            Lifetime = lifetime,
            Type = SessionType.Lobby,
            ExpectedUsers = 0,
        };

        var services = new ServiceCollection();

        services.AddSessionServices(data);
        services.Pass<IOrleans>(_serviceProvider);
        services.Pass<IServiceEnvironment>(_serviceProvider);
        services.Pass<IServiceDiscovery>(_serviceProvider);

        var provider = services.BuildServiceProvider();

        RunSession().NoAwait();

        return data.Id;

        async Task RunSession()
        {
            var session = provider.GetRequiredService<ISession>();
            var serviceFactory = provider.GetRequiredService<IServiceFactory>();

            _collection.Add(session);

            await serviceFactory.OnSessionCreated(lifetime);
            session.Run().NoAwait();
            lifetime.Listen(provider.Dispose);

            _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created", session.Id,
                createOptions
            );
        }
    }

    public Guid CreateMatch(MatchCreateOptions createOptions)
    {
        _logger.LogInformation("[Matchmaking] Creating match: {Type}", createOptions.Type);

        var lifetime = new Lifetime();

        var data = new SessionContainerData
        {
            Id = Guid.NewGuid(),
            Lifetime = lifetime,
            Type = SessionType.Match,
            ExpectedUsers = 2,
        };

        var services = new ServiceCollection();

        services.AddSessionServices(data);

        services.Pass<IOrleans>(_serviceProvider);
        services.Pass<IServiceEnvironment>(_serviceProvider);
        services.Pass<IServiceDiscovery>(_serviceProvider);
        services.Pass<ICardConfigs>(_serviceProvider);

        services.AddCardServices();
        services.AddGameCommands();
        services.AddGameContext();
        services.AddPlayerServices();

        services.Add<RoundPlayers>();

        switch (createOptions.Type)
        {
            case GameMatchType.Single:
                break;
            case GameMatchType.TimeLimited:
                services.Add<TimeLimitedRound>()
                    .As<IService>()
                    .As<IGameRound>();
                break;
            case GameMatchType.LastManStanding:
                services.Add<LastManStandingRound>()
                    .As<IService>()
                    .As<IGameRound>();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        services.Add<ISessionFactory>(this);
        services.Add<MatchHandle>();
        services.Add(createOptions);

        var provider = services.BuildServiceProvider();

        RunSession().NoAwait();

        return data.Id;

        async Task RunSession()
        {
            var session = provider.GetRequiredService<ISession>();
            var serviceFactory = provider.GetRequiredService<IServiceFactory>();

            _collection.Add(session);

            await serviceFactory.OnSessionCreated(lifetime);
            session.Run().NoAwait();
            lifetime.Listen(provider.Dispose);

            await session.AllUsersConnected.WaitInvoke(session.Lifetime);
            var handle = provider.GetRequiredService<MatchHandle>();
            handle.Process().NoAwait();

            _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created",
                session.Id, createOptions
            );
        }
    }
}