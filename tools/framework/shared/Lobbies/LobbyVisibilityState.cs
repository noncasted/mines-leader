using MemoryPack;

namespace Shared
{
    // Lobby service property written by the client of the lobby leader: the "CLOSED / OPEN" switch. A new lobby is
    // closed (entered only by its code); an open one is also listed in the "Connect" window of every player
    // (SharedLobbies.List). The value stays when the leader changes: the next leader becomes its writer.
    [MemoryPackable]
    public partial class LobbyVisibilityState
    {
        public bool IsOpen { get; set; }
    }
}
