namespace Game.Lobbies;

public class LobbyMatch
{
    public required string ServerUrl { get; init; }
    public required Guid SessionId { get; init; }
}
