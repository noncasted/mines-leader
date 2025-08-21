using Common;
using Game.GamePlay;
using Infrastructure.Discovery;
using Infrastructure.Orleans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Services;
using Shared;

namespace Game;

public interface ISessionFactory
{
    Guid Create(SessionCreateOptions createOptions);
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

    public Guid Create(SessionCreateOptions createOptions)
    {
        _logger.LogInformation("[Matchmaking] Creating session with options: {Options}", createOptions);

        var lifetime = new Lifetime();

        var data = new SessionContainerData()
        {
            Id = Guid.NewGuid(),
            Lifetime = lifetime,
            CreateOptions = createOptions,
        };

        var services = new ServiceCollection();

        services.AddSessionServices(data);
        
        services.AddSingleton(_serviceProvider.GetRequiredService<IOrleans>());
        services.AddSingleton(_serviceProvider.GetRequiredService<IServiceEnvironment>());
        services.AddSingleton(_serviceProvider.GetRequiredService<IServiceDiscovery>());

        switch (createOptions.Type)
        {
            case SessionType.Lobby:
                break;
            case SessionType.Game:
                services.AddCardServices();
                services.AddGameCommands();
                services.AddGameContext();
                services.AddPlayerServices();
                services.AddSingleton<ISessionFactory>(this);
                services.Add<MatchHandle>();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

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

            switch (createOptions.Type)
            {
                case SessionType.Lobby:
                    break;
                case SessionType.Game:
                    await session.AllUsersConnected.WaitInvoke(session.Lifetime);
                    var handle = provider.GetRequiredService<MatchHandle>();
                    handle.Process().NoAwait();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created", session.Id,
                createOptions
            );
        }
    }
}