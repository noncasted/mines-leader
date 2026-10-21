using Cluster.Discovery;
using Common.Network;
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
        ISessionTicketIssuer tickets,
        MatchCreateOptions createOptions,
        ILogger<MatchHandle> logger)
    {
        _tickets = tickets;
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
    private readonly ISessionTicketIssuer _tickets;
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

            // Матч с ботом нужно пересоздавать тоже с ботом,
            // иначе новая сессия будет вечно ждать второго живого игрока.
            var bot = _users.FirstOrDefault(t => t.IsBot);
            var humans = _users.Where(t => t.IsBot == false).ToList();

            var rematchOptions = new MatchCreateOptions
            {
                Type = _createOptions.Type
            };

            // S4: the new session reserves the places of the humans of this one (meta ids), each of them gets its
            // own ticket.
            var sessionId = bot != null
                ? _sessionFactory.CreateMatchWithBot(bot.BackendId, rematchOptions, humans.Single().BackendId)
                : _sessionFactory.CreateMatch(rematchOptions, humans.Select(t => t.BackendId).ToList());

            var serviceOverview = _serviceDiscovery.Self as GameServerOverview;
            var serverUrl = serviceOverview!.Url.ServerUrlToWebSocket();

            foreach (var user in humans)
            {
                user.Send(new RematchContexts.Success
                {
                    SessionId = sessionId,
                    ServerUrl = serverUrl,
                    Ticket = _tickets.Issue(user.BackendId, sessionId)
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing game flow");
        }
    }
}