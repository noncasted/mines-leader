using Game.GamePlay;
using Infrastructure.Discovery;
using Microsoft.Extensions.Logging;
using Services;
using Shared;

namespace Game;

public class MatchHandle
{
    public MatchHandle(
        IServiceEnvironment environment,
        IServiceDiscovery serviceDiscovery,
        IGameFlow gameFlow,
        ISessionUsers users,
        ISessionFactory sessionFactory,
        ILogger<MatchHandle> logger)
    {
        _environment = environment;
        _serviceDiscovery = serviceDiscovery;
        _gameFlow = gameFlow;
        _users = users;
        _sessionFactory = sessionFactory;
        _logger = logger;
    }

    private readonly IServiceEnvironment _environment;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IGameFlow _gameFlow;
    private readonly ISessionUsers _users;
    private readonly ISessionFactory _sessionFactory;
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

            var sessionId = _sessionFactory.Create(new SessionCreateOptions
                {
                    ExpectedUsers = 2,
                    Type = SessionType.Game
                }
            );

            var serviceOverview = _serviceDiscovery.Self as GameServerOverview;

            var rematchSuccess = new RematchContexts.Success
            {
                SessionId = sessionId,
                ServerUrl = _environment.ServerUrlToWebSocket(serviceOverview!.Url)
            };

            _users.SendAll(rematchSuccess);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing game flow");
        }
    }
}