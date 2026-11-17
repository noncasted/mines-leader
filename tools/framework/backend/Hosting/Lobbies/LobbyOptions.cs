namespace Game.Lobbies;

// The lobbies of a game server.
public class LobbyOptions
{
    // The places of a lobby and of the match it starts: the lobby screen and the match of the game are built for
    // that many.
    public required int MaxUsers { get; init; }
}
