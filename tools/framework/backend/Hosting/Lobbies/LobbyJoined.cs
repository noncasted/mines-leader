namespace Game.Lobbies;

public class LobbyJoined
{
    public required LobbyJoinStatus Status { get; init; }

    // Ok only: the session to enter (the lobby or the lobby of the late players).
    public Guid SessionId { get; init; }

    internal static LobbyJoined Refused(LobbyJoinStatus status)
    {
        return new LobbyJoined
        {
            Status = status
        };
    }
}
