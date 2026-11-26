namespace Cluster.Lobbies;

// The state of a lobby (or of the match it has started) for the list of open lobbies, reported by the game server.
[GenerateSerializer]
public class LobbyListing
{
    // The leader has opened the lobby (LobbyVisibilityState); a match keeps the value its lobby had at the start.
    [Id(0)] public bool IsOpen { get; init; }

    // The leader of the lobby / the owner of the match; empty while nobody is connected.
    [Id(1)] public Guid LeaderUserId { get; init; }

    [Id(2)] public int PlayersCount { get; init; }

    // The match of the lobby is going.
    [Id(3)] public bool IsRunning { get; init; }

    // Id 4 removed, kept for stored values.

    // Id 5 removed (IsJoinable: when a running match is entered is decided by ILobbyEntryPolicy), kept for stored values.

    // The places of the lobby (and of its match): the game decides (LobbyOptions.MaxUsers).
    [Id(6)] public int MaxPlayers { get; init; }

    public bool IsListed => IsOpen == true &&
                            PlayersCount > 0 &&
                            PlayersCount < MaxPlayers &&
                            LeaderUserId != Guid.Empty;
}
