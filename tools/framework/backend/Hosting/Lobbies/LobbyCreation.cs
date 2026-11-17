namespace Game.Lobbies;

public class LobbyCreation
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required int MaxUsers { get; init; }

    // The lobby of the late players of a running match (see LobbySessionInfo).
    public Guid LobbySessionId { get; init; }
    public Guid RunningMatchId { get; init; }
}
