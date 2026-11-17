namespace Game.Hosting;

// The search of the game: the session to enter with a place reserved for the user (see ISessionAdmission), a public
// match with a free place or the lobby of the late players of a running one (isLobby), otherwise a new public match.
// Lobbies by code and other private sessions are never found. Implemented by the game: it knows when a running match
// can be entered and builds the containers of its sessions.
public interface ISessionSearch
{
    Guid GetOrCreate(Guid userId, out bool isLobby);
}
