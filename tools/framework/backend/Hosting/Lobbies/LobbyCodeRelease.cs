namespace Game.Lobbies;

// What becomes of the code of a lobby after its match (LobbyCodeLeases tells it): released with the match
// (LobbyCodeRelease) or returned to a new lobby (LobbyReturns, LobbyOptions.ReturnsAfterMatch). Called outside the lock of
// the leases: from the queue of the match or from the end of its lifetime.
public interface ILobbyCodeAfterMatch
{
    // The first player of the match has left it for good (a lost connection that waits for its return is not a leave);
    // once per match, also when the leave comes with the close.
    void OnMatchLeft(LobbyMatchCode code);

    // The match is closed.
    void OnMatchClosed(LobbyMatchCode code);
}

// The default: the code stays with the match until it is closed, then goes back to the registry.
public class LobbyCodeRelease : ILobbyCodeAfterMatch
{
    public void OnMatchLeft(LobbyMatchCode code)
    {
        // The others go on playing: the code still leads to the match.
    }

    public void OnMatchClosed(LobbyMatchCode code)
    {
        code.Release();
    }
}
