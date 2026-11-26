using MemoryPack;

namespace Shared
{
    // Lobby service property written only by the backend: the code of the room and the index of the
    // lobby leader (the session owner, 0 while nobody is connected).
    [MemoryPackable]
    public partial class LobbyState
    {
        public string Code { get; set; } = string.Empty;
        public int OwnerIndex { get; set; }

        // The lobby of the late players of a match that is already going (the code belongs to the match; a match
        // found by the search has no code): no leader start and no countdown. When the late players get a place in
        // the match is up to the game (a derived LobbyService): it writes the match to LobbyGameState then.
        public bool IsRunning { get; set; }

        // IsRunning only: the match has ended before the late players got in: nobody enters it any more.
        public bool IsRunOver { get; set; }
    }
}
