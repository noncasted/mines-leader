namespace Game.Lobbies;

// When a running match lets a new player in, decided by the game (the match may be entered only at a rest, never
// after its finale): asked by the lobby sessions before a place in the match is reserved.
public interface ILobbyEntryPolicy
{
    // The match has ended: the lobby of its late players goes back, the code answers NotFound.
    bool IsMatchOver(Guid matchSessionId);

    // A late player may take a place in the match now (a join by its code is answered Started otherwise).
    bool CanEnterMatchNow(Guid matchSessionId);
}

public static class LobbyEntryPolicy
{
    // Every running match can be entered at any time until it is closed.
    public sealed class Always : ILobbyEntryPolicy
    {
        public bool IsMatchOver(Guid matchSessionId)
        {
            return false;
        }

        public bool CanEnterMatchNow(Guid matchSessionId)
        {
            return true;
        }
    }
}
