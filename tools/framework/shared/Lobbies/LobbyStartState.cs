using MemoryPack;

namespace Shared
{
    // Lobby service property written by the client of the lobby leader: "Start" sets IsStarting, "Cancel"
    // clears it. The backend clears it only when the leader (the writer) changes while it is set.
    [MemoryPackable]
    public partial class LobbyStartState
    {
        public bool IsStarting { get; set; }
    }
}
