using Backend.Users;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Matches;

public class MatchFactory : IMatchFactory
{
    public MatchFactory(
        IClusterClient orleans,
        ITransactions transactions,
        IServers servers,
        IMessagingClient messaging,
        IServiceEnvironment environment,
        ILogger<MatchFactory> logger)
    {
        _orleans = orleans;
        _transactions = transactions;
        _servers = servers;
        _messaging = messaging;
        _environment = environment;
        _logger = logger;
    }

    private readonly IClusterClient _orleans;
    private readonly ITransactions _transactions;
    private readonly IServers _servers;
    private readonly IMessagingClient _messaging;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<MatchFactory> _logger;

    public async Task Create(IReadOnlyList<Guid> participants)
    {
        var match = _orleans.GetGrain<IMatch>(Guid.NewGuid());

        await _transactions.Create(() => match.Setup(GameMatchType.Single, participants));

        var targetServer = _servers.Entries.Random();

        var request = new MatchPayloads.Create.Request
        {
            Type = MatchmakingConstants.GameType,
            ExpectedUsers = participants.Count,
        };

        var response = await _messaging.Send<MatchPayloads.Create.Response>(targetServer.ClientId, request);

        _logger.LogInformation("[MatchFactory] Created match {MatchID} on server {ServerURL}",
            match.GetPrimaryKey(),
            targetServer.Url);

        var result = new MatchSearchResultUpdate()
        {
            SessionId = response.SessionId,
            ServerUrl = _environment.ServerUrlToWebSocket(targetServer.Url)
        };

        foreach (var participant in participants)
            await _orleans.SendOneTimeProjection(participant, result);
    }
}