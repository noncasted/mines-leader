namespace Cluster.Lobbies;

[GenerateSerializer]
public class OpenLobby
{
    [Id(0)] public string Code { get; init; } = string.Empty;
    [Id(1)] public Guid ServerId { get; init; }
    [Id(2)] public LobbyListing Listing { get; init; } = new();
}
