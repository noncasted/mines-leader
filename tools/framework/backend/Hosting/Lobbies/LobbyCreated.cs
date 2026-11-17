namespace Game.Lobbies;

public class LobbyCreated
{
    public required Guid SessionId { get; init; }
    public required string Code { get; init; }
}
