using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Game;

public class SessionFactory : ISessionFactory
{
    public SessionFactory(
        IServiceScopeFactory scopeFactory,
        ISessionsCollection collection,
        ILogger<SessionFactory> logger)
    {
        _scopeFactory = scopeFactory;
        _collection = collection;
        _logger = logger;
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISessionsCollection _collection;
    private readonly ILogger<SessionFactory> _logger;

    public async Task<Guid> Create(SessionCreateOptions createOptions)
    {
        _logger.LogInformation("[Matchmaking] Creating session with options: {Options}", createOptions);

        var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var lifetime = new Lifetime();

        var sessionFactory = services.GetRequiredService<Func<SessionContainerData, ISession>>();

        var session = sessionFactory.Invoke(new SessionContainerData()
        {
            Id = Guid.NewGuid(),
            Lifetime = lifetime,
            CreateOptions = createOptions,
            Scope = scope
        });

        _collection.Add(session);
        lifetime.Listen(scope.Dispose);

        session.Run().NoAwait();

        _logger.LogInformation("[Matchmaking] Session {ID} with options {Options} created", session.Id, createOptions);

        return session.Id;
    }
}