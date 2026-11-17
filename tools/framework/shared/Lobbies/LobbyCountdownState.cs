using MemoryPack;

namespace Shared
{
    // Lobby service property written only by the backend: the start countdown it runs while the leader
    // keeps LobbyStartState.IsStarting. SecondsLeft goes from Seconds down to 0, one step a second.
    [MemoryPackable]
    public partial class LobbyCountdownState
    {
        public const int Seconds = 5;

        public bool IsActive { get; set; }
        public int SecondsLeft { get; set; }
    }
}
