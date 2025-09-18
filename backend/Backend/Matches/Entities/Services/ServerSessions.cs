using Common;
using Game;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Services;

namespace Backend.Matches;

public class ServerSessions : BackgroundService
{
    public ServerSessions(
        IMessaging messaging,
        ISessionFactory sessionFactory,
        IServiceDiscovery serviceDiscovery,
        ISessionSearch sessionSearch)
    {
        _messaging = messaging;
        _sessionFactory = sessionFactory;
        _serviceDiscovery = serviceDiscovery;
        _sessionSearch = sessionSearch;
    }

    private readonly IMessaging _messaging;
    private readonly ISessionFactory _sessionFactory;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly ISessionSearch _sessionSearch;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();

        _messaging.AddPipeRequestHandler<
            MatchPayloads.Create.Request,
            MatchPayloads.Create.Response>(
            lifetime,
            new MessagePipeServiceRequestId(_serviceDiscovery.Self, typeof(MatchPayloads.Create.Request)),
            CreateSession
        );

        _messaging.AddPipeRequestHandler<
            MatchPayloads.GetOrCreate.Request,
            MatchPayloads.GetOrCreate.Response>(
            lifetime,
            new MessagePipeServiceRequestId(_serviceDiscovery.Self, typeof(MatchPayloads.GetOrCreate.Request)),
            GetOrCreateSession
        );

        return Task.CompletedTask;

        Task<MatchPayloads.Create.Response> CreateSession(MatchPayloads.Create.Request request)
        {
            var id = _sessionFactory.Create(new SessionCreateOptions
                {
                    ExpectedUsers = request.ExpectedUsers,
                    Type = request.Type
                }
            );

            return Task.FromResult(new MatchPayloads.Create.Response
                {
                    SessionId = id
                }
            );
        }

        Task<MatchPayloads.GetOrCreate.Response> GetOrCreateSession(MatchPayloads.GetOrCreate.Request request)
        {
            var id = _sessionSearch.GetOrCreate(new SessionSearchParameters
                {
                    Type = request.Type
                }
            );

            return Task.FromResult(new MatchPayloads.GetOrCreate.Response
                {
                    SessionId = id
                }
            );
        }
    }
}