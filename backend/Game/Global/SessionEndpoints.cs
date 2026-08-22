using Cluster.Coordination;
using Cluster.Discovery;
using Common.Reactive;
using Game.Session;
using Infrastructure;
using Meta.Matches;
using Microsoft.Extensions.Logging;

namespace Game.Global;

public class SessionEndpoints : ICoordinatorSetupCompleted
{
    public SessionEndpoints(
        IMessaging messaging,
        ISessionFactory sessionFactory,
        IServiceDiscovery serviceDiscovery,
        ISessionSearch sessionSearch,
        ILogger<SessionEndpoints> logger)
    {
        _messaging = messaging;
        _sessionFactory = sessionFactory;
        _serviceDiscovery = serviceDiscovery;
        _sessionSearch = sessionSearch;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly ISessionFactory _sessionFactory;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly ISessionSearch _sessionSearch;
    private readonly ILogger<SessionEndpoints> _logger;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _messaging.AddPipeRequestHandler<
            MatchPayloads.Match.Request,
            MatchPayloads.Match.Response>(lifetime,
            new MessagePipeServiceRequestId(_serviceDiscovery.Self, typeof(MatchPayloads.Match.Request)),
            CreateMatch);

        _messaging.AddPipeRequestHandler<
            MatchPayloads.Match.RequestWithBot,
            MatchPayloads.Match.Response>(lifetime,
            new MessagePipeServiceRequestId(_serviceDiscovery.Self, typeof(MatchPayloads.Match.RequestWithBot)),
            CreateMatchWithBot);

        _messaging.AddPipeRequestHandler<
            MatchPayloads.Lobby.Request,
            MatchPayloads.Lobby.Response>(lifetime,
            new MessagePipeServiceRequestId(_serviceDiscovery.Self, typeof(MatchPayloads.Lobby.Request)),
            GetOrCreateLobby);

        return Task.CompletedTask;

        Task<MatchPayloads.Match.Response> CreateMatch(MatchPayloads.Match.Request request)
        {
            var id = _sessionFactory.CreateMatch(new MatchCreateOptions
            {
                Type = request.Type,
            });

            return Task.FromResult(new MatchPayloads.Match.Response
            {
                SessionId = id
            });
        }

        Task<MatchPayloads.Match.Response> CreateMatchWithBot(MatchPayloads.Match.RequestWithBot request)
        {
            var id = _sessionFactory.CreateMatchWithBot(request.BotId, new MatchCreateOptions
            {
                Type = request.Type,
                Fixture = request.Fixture?.ToShared(),
            });

            return Task.FromResult(new MatchPayloads.Match.Response
            {
                SessionId = id
            });
        }

        Task<MatchPayloads.Lobby.Response> GetOrCreateLobby(MatchPayloads.Lobby.Request request)
        {
            _logger.LogInformation("{UserId} [Lobby] [Game] GetOrCreate session request received",
                request.UserId);

            var id = _sessionSearch.GetOrCreateLobby();

            _logger.LogInformation("{UserId} [Lobby] [Game] GetOrCreate session returning session {SessionID}",
                request.UserId,
                id);

            return Task.FromResult(new MatchPayloads.Lobby.Response
            {
                SessionId = id
            });
        }

    }
}