using Backend.Users;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;
using Services;
using Shared;

namespace Backend.Matches;

public class MatchFactory : IMatchFactory
{
    public MatchFactory(
        IOrleans orleans,
        IMessaging messaging,
        IServiceDiscovery serviceDiscovery,
        IServiceEnvironment environment,
        ILogger<MatchFactory> logger)
    {
        _orleans = orleans;
        _messaging = messaging;
        _serviceDiscovery = serviceDiscovery;
        _orleans = orleans;
        _environment = environment;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<MatchFactory> _logger;

    public async Task Create(IReadOnlyList<Guid> participants)
    {
        var match = _orleans.GetGrain<IMatch>(Guid.NewGuid());

        await _orleans.InTransaction(() => match.Setup(GameMatchType.Single, participants));

        var targetServer = _serviceDiscovery.RandomServer();

        var request = new MatchPayloads.Create.Request
        {
            Type = SessionType.Game,
            ExpectedUsers = participants.Count,
        };
        
        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<MatchPayloads.Create.Response>(pipeId, request);

        _logger.LogInformation("[MatchFactory] Created match {MatchID} on server {ServerURL}",
            match.GetPrimaryKey(),
            targetServer.Url);

        var result = new MatchSearchResultUpdate()
        {
            SessionId = response.SessionId,
            ServerUrl = _environment.ServerUrlToWebSocket(targetServer.Url)
        };

        foreach (var participant in participants)
            await _orleans.Grains.SendOneTimeProjection(participant, result);
    }
}