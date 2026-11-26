namespace Game.Lobbies;

// The lobbies of a game server.
public class LobbyOptions
{
    // The places of a lobby and of the match it starts: the lobby screen and the match of the game are built for
    // that many.
    public required int MaxUsers { get; init; }

    // After the match of a lobby its code comes back to a new lobby (LobbyCreation.Return). Off: the code is
    // released with the match, as before.
    public bool ReturnsAfterMatch { get; init; }
}
