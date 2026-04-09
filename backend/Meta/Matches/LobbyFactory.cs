using Cluster.Coordination;
using Cluster.Discovery;
using Infrastructure;
using Meta.Users;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Matches;

public interface ILobbyFactory
{
    Task GetOrCreate(Guid userId);
}

public class LobbyFactory : ILobbyFactory
{
    public LobbyFactory(
        IOrleans orleans,
        IServiceDiscovery serviceDiscovery,
        IMessaging messaging,
        IServiceEnvironment environment,
        ILogger<LobbyFactory> logger)
    {
        _orleans = orleans;
        _serviceDiscovery = serviceDiscovery;
        _messaging = messaging;
        _environment = environment;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IMessaging _messaging;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<LobbyFactory> _logger;

    public async Task GetOrCreate(Guid userId)
    {
        var targetServer = _serviceDiscovery.RandomServer();

        var request = new MatchPayloads.Lobby.Request
        {
            Type = SessionType.Lobby,
            UserId = userId,
        };

        _logger.LogInformation("{UserId} [Lobby] [Meta] Requesting lobby from server {ServerURL}",
            userId,
            targetServer.Url.ServerUrlToWebSocket());

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<MatchPayloads.Lobby.Response>(pipeId, request);

        var serverUrl = targetServer.Url.ServerUrlToWebSocket();

        var result = new LobbySearchResultUpdate()
        {
            SessionId = response.SessionId,
            ServerUrl = serverUrl
        };

        _logger.LogInformation("{UserId} [Lobby] [Meta] Received lobby {SessionID} on server {ServerURL}",
            userId,
            response.SessionId,
            serverUrl);

        await _orleans.SendOneTimeProjection(userId, result);

        _logger.LogInformation("{UserId} [Lobby] [Meta] Sent lobby search result projection",
            userId);
    }
}