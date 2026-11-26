using Cluster.Lobbies;

namespace Game.Lobbies;

// The list of open lobbies, fed by the sessions of a lobby code: the lobby reports itself until it starts, then its
// match does (by the id of the lobby session that holds the code). The reports of one code go to the registry in
// order, only the latest of a burst (LobbyCodeLeases).
public interface ILobbyListings
{
    void Report(Guid lobbySessionId, LobbyListing listing);
}
