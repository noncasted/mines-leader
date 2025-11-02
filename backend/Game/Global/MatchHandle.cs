using Cluster.Discovery;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Global;

public class MatchHandle
{
    public MatchHandle(
        IServiceDiscovery serviceDiscovery,
        IGameFlow gameFlow,
        ISessionUsers users,
        ISessionFactory sessionFactory,
        MatchCreateOptions createOptions,
        ILogger<MatchHandle> logger)
    {
        _serviceDiscovery = serviceDiscovery;
        _gameFlow = gameFlow;
        _users = users;
        _sessionFactory = sessionFactory;
        _createOptions = createOptions;
        _logger = logger;
    }

    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IGameFlow _gameFlow;
    private readonly ISessionUsers _users;
    private readonly ISessionFactory _sessionFactory;
    private readonly MatchCreateOptions _createOptions;
    private readonly ILogger<MatchHandle> _logger;

    public async Task Process()
    {
        try
        {
            var transition = await _gameFlow.Process();

            if (transition == MatchTransitionResult.End)
            {
                var rematchFailed = new RematchContexts.Failure();
                _users.SendAll(rematchFailed);
                return;
            }

            var sessionId = _sessionFactory.CreateMatch(new MatchCreateOptions
                {
                    Type = _createOptions.Type
                }
            );

            var serviceOverview = _serviceDiscovery.Self as GameServerOverview;

            var rematchSuccess = new RematchContexts.Success
            {
                SessionId = sessionId,
                ServerUrl = serviceOverview!.Url.ServerUrlToWebSocket()
            };

            _users.SendAll(rematchSuccess);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing game flow");
        }
    }
}