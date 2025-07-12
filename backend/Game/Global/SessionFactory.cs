using Common;
using Game.GamePlay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game;

public interface ISessionFactory
{
    Guid Create(SessionCreateOptions createOptions);
}

public class SessionFactory : ISessionFactory
{
    public SessionFactory(
        ISessionsCollection collection,
        ILogger<SessionFactory> logger)
    {
        _collection = collection;
        _logger = logger;
    }

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

        switch (createOptions.Type)
        {
            case SessionType.Lobby:
                break;
            case SessionType.Game:
                services.AddCardServices();
                services.AddGameCommands();
                services.AddGameContext();
                services.AddPlayerServices();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        var provider = services.BuildServiceProvider();

        var session = provider.GetRequiredService<ISession>();
        
        _collection.Add(session);

        session.Run().NoAwait();
        lifetime.Listen(provider.Dispose);

        _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created", session.Id, createOptions);

        return session.Id;
    }
}