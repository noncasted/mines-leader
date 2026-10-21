using Cluster.Coordination;
using Cluster.Discovery;
using Common.Network;
using Infrastructure;
using Meta.Bots;
using Meta.Users;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Matches;

// Creates the session and issues the tickets; delivering the result to the participants is up to the caller:
// a command response (Create, CreateWithBot) or a search result projection with the search id (Matchmaking).
public interface IMatchFactory
{
    Task<CreatedMatch> Create(IReadOnlyList<Guid> participants, GameMatchType type);
    Task<CreatedMatch> CreateWithBot(Guid participant, GameMatchType type, AgentMatchFixture? fixture = null);
}

public class CreatedMatch
{
    public required Guid SessionId { get; init; }
    public required string ServerUrl { get; init; }
    public required GameMatchType Type { get; init; }

    // S4: the entry of each human participant into the session (SessionTickets).
    public required IReadOnlyDictionary<Guid, string> Tickets { get; init; }

    public SharedMatchmaking.MatchResult ToResult(Guid userId, Guid requestId)
    {
        return new SharedMatchmaking.MatchResult
        {
            SessionId = SessionId,
            ServerUrl = ServerUrl,
            Type = Type,
            Ticket = Tickets[userId],
            RequestId = requestId,
            Status = SharedMatchmaking.MatchSearchStatus.Matched
        };
    }
}

public class MatchFactory : IMatchFactory
{
    public MatchFactory(
        IOrleans orleans,
        IMessaging messaging,
        IServiceDiscovery serviceDiscovery,
        IBotCollection botCollection,
        IBotFactory botFactory,
        ISessionTicketIssuer tickets,
        ILogger<MatchFactory> logger)
    {
        _tickets = tickets;
        _orleans = orleans;
        _messaging = messaging;
        _serviceDiscovery = serviceDiscovery;
        _botCollection = botCollection;
        _botFactory = botFactory;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IBotCollection _botCollection;
    private readonly IBotFactory _botFactory;
    private readonly ISessionTicketIssuer _tickets;
    private readonly ILogger<MatchFactory> _logger;

    public async Task<CreatedMatch> Create(IReadOnlyList<Guid> participants, GameMatchType type)
    {
        var match = _orleans.GetGrain<IMatch>(Guid.NewGuid());
        await _orleans.InTransaction(() => match.Setup(GameMatchType.Single, participants));

        var targetServer = _serviceDiscovery.RandomServer();

        var request = new MatchPayloads.Match.Request
        {
            Type = type,
            Participants = participants
        };

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<MatchPayloads.Match.Response>(pipeId, request);

        _logger.LogInformation("[MatchFactory] Created match {MatchID} on server {ServerURL}",
            match.GetPrimaryKey(), targetServer.Url);

        return new CreatedMatch
        {
            SessionId = response.SessionId,
            ServerUrl = targetServer.Url.ServerUrlToWebSocket(),
            Type = type,
            Tickets = participants.ToDictionary(participant => participant,
                participant => _tickets.Issue(participant, response.SessionId))
        };
    }

    public async Task<CreatedMatch> CreateWithBot(Guid participant, GameMatchType type, AgentMatchFixture? fixture = null)
    {
        var botId = GetRandomBotId();

        // Колода бота перевыдаётся из актуального конфига перед каждым матчем.
        if (botId != Guid.Empty)
            await _botFactory.AssignDeck(botId);

        var match = _orleans.GetGrain<IMatch>(Guid.NewGuid());
        await _orleans.InTransaction(() => match.Setup(GameMatchType.Single, new[] { participant, botId }));

        var targetServer = _serviceDiscovery.RandomServer();

        var request = new MatchPayloads.Match.RequestWithBot
        {
            Type = type,
            BotId = botId,
            Fixture = MatchPayloads.Match.FixturePayload.From(fixture),
            Participant = participant
        };

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<MatchPayloads.Match.Response>(pipeId, request);

        _logger.LogInformation("[MatchFactory] Created match {MatchID} on server {ServerURL} with bot {BotID}",
            match.GetPrimaryKey(), targetServer.Url, botId);

        return new CreatedMatch
        {
            SessionId = response.SessionId,
            ServerUrl = targetServer.Url.ServerUrlToWebSocket(),
            Type = type,
            Tickets = new Dictionary<Guid, string> { [participant] = _tickets.Issue(participant, response.SessionId) }
        };
    }

    private Guid GetRandomBotId()
    {
        if (_botCollection.Count == 0)
        {
            _logger.LogWarning("[MatchFactory] No bots available, using Guid.Empty as fallback");
            return Guid.Empty;
        }

        var botIds = _botCollection.Keys.ToList();
        var randomIndex = Random.Shared.Next(botIds.Count);
        return botIds[randomIndex];
    }
}