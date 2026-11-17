namespace Cluster.Lobbies;

// The open lobbies of a deploy, pushed by the registry on OpenLobbiesChannelId after every change.
[GenerateSerializer]
public class OpenLobbies
{
    // Grows with every change: a listener drops a snapshot older than the one it has.
    [Id(0)] public long Version { get; init; }
    [Id(1)] public List<OpenLobby> Entries { get; init; } = new();
}
