using System;
using MemoryPack;

namespace Shared
{
    // Lobby service property written only by the backend: the match session every user of the lobby has a place in.
    // Written once, when the countdown reaches zero; the lobby of the late players of a running match writes it when
    // the match lets them in and clears it after. Empty SessionId - the lobby has not started a game.
    [MemoryPackable]
    public partial class LobbyGameState
    {
        public string ServerUrl { get; set; } = string.Empty;
        public Guid SessionId { get; set; }

        // Users the match has a place for: every client of the match waits for that many players before it starts.
        public int PlayersCount { get; set; }
    }
}
