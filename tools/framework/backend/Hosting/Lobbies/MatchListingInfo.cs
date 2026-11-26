namespace Game.Lobbies;

// What the listing service of a match started by a lobby knows about its match.
public class MatchListingInfo
{
    // The lobby session that holds the code (the key of the listing reports).
    public required Guid LobbySessionId { get; init; }

    // The lobby was open at the start: the match is listed too.
    public required bool IsOpen { get; init; }

    public required int MaxUsers { get; init; }
}
