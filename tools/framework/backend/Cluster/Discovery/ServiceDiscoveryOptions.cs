namespace Cluster.Discovery;

public class ServiceDiscoveryOptions
{
    // The service is a game server: it publishes its public url (GAME_SERVER_URL), matchmaking hands it to clients.
    // Set by the composition root for the game server role (PublishGameServerUrl).
    public bool PublishGameServerUrl { get; init; }
}
