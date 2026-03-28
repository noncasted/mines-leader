using Cluster.Coordination;
using Cluster.Discovery;
using Infrastructure;
using Meta.Bots;
using Meta.Users;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Matches;

public interface IMatchFactory
{
    Task Create(IReadOnlyList<Guid> participants, GameMatchType type);
    Task CreateWithBot(Guid participant, GameMatchType type);
}

public class MatchFactory : IMatchFactory
{
    public MatchFactory(
        IOrleans orleans,
        IMessaging messaging,
        IServiceDiscovery serviceDiscovery,
        IBotCollection botCollection,
        ILogger<MatchFactory> logger)
    {
        _orleans = orleans;
        _messaging = messaging;
        _serviceDiscovery = serviceDiscovery;
        _botCollection = botCollection;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IBotCollection _botCollection;
    private readonly ILogger<MatchFactory> _logger;

    public async Task Create(IReadOnlyList<Guid> participants, GameMatchType type)
    {
        var match = _orleans.GetGrain<IMatch>(Guid.NewGuid());
        await _orleans.InTransaction(() => match.Setup(GameMatchType.Single, participants));

        var targetServer = _serviceDiscovery.RandomServer();

        var request = new MatchPayloads.Match.Request
        {
            Type = type
        };

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<MatchPayloads.Match.Response>(pipeId, request);

        _logger.LogInformation("[MatchFactory] Created match {MatchID} on server {ServerURL}",
            match.GetPrimaryKey(), targetServer.Url
        );

        var result = new MatchSearchResultUpdate
        {
            SessionId = response.SessionId,
            ServerUrl = targetServer.Url.ServerUrlToWebSocket(),
            Type = type
        };

        foreach (var participant in participants)
            await _orleans.SendOneTimeProjection(participant, result);
    }

    public async Task CreateWithBot(Guid participant, GameMatchType type)
    {
        var botId = GetRandomBotId();
        var match = _orleans.GetGrain<IMatch>(Guid.NewGuid());
        await _orleans.InTransaction(() => match.Setup(GameMatchType.Single, new[] { participant, botId }));

        var targetServer = _serviceDiscovery.RandomServer();

        var request = new MatchPayloads.Match.RequestWithBot
        {
            Type = type,
            BotId = botId
        };

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<MatchPayloads.Match.Response>(pipeId, request);

        _logger.LogInformation("[MatchFactory] Created match {MatchID} on server {ServerURL} with bot {BotID}",
            match.GetPrimaryKey(), targetServer.Url, botId
        );

        var result = new MatchSearchResultUpdate
        {
            SessionId = response.SessionId,
            ServerUrl = targetServer.Url.ServerUrlToWebSocket(),
            Type = type
        };

        await _orleans.SendOneTimeProjection(participant, result);
    }

    private Guid GetRandomBotId() {
        if (_botCollection.Count == 0) {
            _logger.LogWarning("[MatchFactory] No bots available, using Guid.Empty as fallback");
            return Guid.Empty;
        }

        var botIds = _botCollection.Keys.ToList();
        var randomIndex = Random.Shared.Next(botIds.Count);
        return botIds[randomIndex];
    }
}