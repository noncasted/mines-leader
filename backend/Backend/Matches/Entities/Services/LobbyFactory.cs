using Backend.Users.Projections;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Matches;

public class LobbyFactory : ILobbyFactory
{
    public LobbyFactory(
        IClusterClient orleans,
        IServers servers,
        IMessagingClient messaging,
        IServiceEnvironment environment,
        ILogger<LobbyFactory> logger)
    {
        _orleans = orleans;
        _servers = servers;
        _messaging = messaging;
        _environment = environment;
        _logger = logger;
    }

    private readonly IClusterClient _orleans;
    private readonly IServers _servers;
    private readonly IMessagingClient _messaging;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<LobbyFactory> _logger;

    public async Task GetOrCreate(Guid userId)
    {
        var targetServer = _servers.Entries.Random();

        var request = new MatchPayloads.GetOrCreate.Request()
        {
            Type = MatchmakingConstants.LobbyType,
        };

        var response = await _messaging.Send<MatchPayloads.GetOrCreate.Response>(targetServer.ClientId, request);

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