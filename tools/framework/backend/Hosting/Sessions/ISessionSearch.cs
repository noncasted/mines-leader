namespace Game.Hosting;

// The search of the game: the session to enter with a place reserved for the user (see ISessionAdmission), a public
// match with a free place or the lobby of the late players of a running one (isLobby), otherwise a new public match.
// Lobbies by code and other private sessions are never found. Implemented by the game: it knows when a running match
// can be entered and builds the containers of its sessions.
public interface ISessionSearch
{
    // queue: the queue of the search (SharedSessionRequests.Search.Queue, 0 by default) - a session found or created
    // for one queue is never given to another. What a queue means is up to the game.
    Guid GetOrCreate(Guid userId, int queue, out bool isLobby);
}
