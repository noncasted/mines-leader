using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSessionServices(data);
        var provider = serviceCollection.BuildServiceProvider();

        var session = provider.GetRequiredService<ISession>();
        
        _collection.Add(session);

        session.Run().NoAwait();
        lifetime.Listen(provider.Dispose);

        _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created", session.Id, createOptions);

        return session.Id;
    }
}