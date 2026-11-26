namespace Game.Lobbies;

// Where a returned lobby comes from: the lobby that started the match, the match, the leader at the start. The game
// takes what the new lobby keeps of the old one (its own properties) from its record of that match.
public class LobbyReturn
{
    public required Guid LobbySessionId { get; init; }
    public required Guid MatchSessionId { get; init; }
    public required Guid LeaderId { get; init; }
}
