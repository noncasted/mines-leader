namespace Shared
{
    // Network service "Lobby" of a lobby session: the key and the property ids shared by the client
    // (LobbyNetworkService of the lobbies module) and the backend (Game.Lobbies.LobbyService). Ids are part of the
    // protocol: never renumber or reuse them. The properties of the game (a derived LobbyService) start at
    // GamePropertyFirst.
    public static class SharedLobbyService
    {
        public const string Key = "Lobby";

        // LobbyState
        public const int LobbyPropertyId = 1;

        // LobbyStartState
        public const int StartPropertyId = 2;

        // LobbyCountdownState
        public const int CountdownPropertyId = 3;

        // LobbyGameState
        public const int GamePropertyId = 4;

        // LobbyVisibilityState
        public const int VisibilityPropertyId = 5;

        public const int GamePropertyFirst = 100;
    }
}
