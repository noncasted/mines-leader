using Backend.Users;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Services;
using Shared;

namespace Backend.Matches;

public class LobbyFactory : ILobbyFactory
{
    public LobbyFactory(
        IClusterClient orleans,
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

    private readonly IClusterClient _orleans;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IMessaging _messaging;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<LobbyFactory> _logger;

    public async Task GetOrCreate(Guid userId)
    {
        var targetServer = _serviceDiscovery.RandomServer();

        var request = new MatchPayloads.GetOrCreate.Request()
        {
            Type = SessionType.Lobby,
        };

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<MatchPayloads.GetOrCreate.Response>(pipeId, request);

        var serverUrl = _environment.ServerUrlToWebSocket(targetServer.Url);
        
        var result = new LobbySearchResultUpdate()
        {
            SessionId = response.SessionId,
            ServerUrl = serverUrl
        };

        _logger.LogInformation("[LobbyFactory] Received lobby {SessionID} on server {ServerURL}",
            response.SessionId,
            serverUrl);

        await _orleans.SendOneTimeProjection(userId, result);
    }
}