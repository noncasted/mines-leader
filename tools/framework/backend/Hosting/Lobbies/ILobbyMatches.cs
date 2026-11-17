namespace Game.Lobbies;

// What a lobby session needs from its game server when the start countdown reaches zero: a match session
// with a place for every user of the lobby. Implemented by the game (it builds the match with its options and passes
// the code to the match: ILobbyCodeLeases.PassToMatch).
public interface ILobbyMatches
{
    // Private match on this server, a place is reserved for each of the users (SessionAdmission.JoinTimeout).
    // The code of the lobby passes to the match: it stays taken until the match is closed.
    // leaderId: the leader of the lobby at the start (its session owner, one of userIds): a game that hosts the match on
    // a client (P2P) gives that role to it.
    // isOpen: the lobby is listed among the open lobbies at the start - so is its match.
    LobbyMatch Create(Guid lobbySessionId, Guid leaderId, IReadOnlyList<Guid> userIds, bool isOpen);

    // The lobby of the late players of a running match on this server: a place is reserved for the user
    // (SessionAdmission.JoinTimeout). Null when the match is closed or has no free place.
    LobbyMatch? Admit(Guid matchSessionId, Guid userId);
}
