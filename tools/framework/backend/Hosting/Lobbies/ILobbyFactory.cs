using Game.Session;

namespace Game.Lobbies;

// Creates the lobby sessions of this game server: a session of type Lobby with the places of LobbyCreation, private,
// nobody expected (ExpectedUsers 0), with a LobbyService (AddLobbyService) in its container. Implemented by the game.
public interface ILobbyFactory
{
    ISession Create(LobbyCreation creation);
}
