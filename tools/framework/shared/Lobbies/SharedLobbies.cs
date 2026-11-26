using System.Collections.Generic;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // Lobby rooms of the meta: creation, entry by a code, the list of open lobbies and the entry into a match that is
    // already going. Union tags: NetworkContextTags.Lobby* (the project lists them in its union). Every entry is
    // answered by SharedSessionRequests.Result.
    public static partial class SharedLobbies
    {
        // Creates a private lobby session, the caller gets a place in it and its code.
        [MemoryPackable]
        public partial class CreateLobby : INetworkContext
        {
        }

        // Enters a lobby session by its code (see LobbyCode). The lobby of a code that has started its match leads to
        // the lobby of the late players of that match.
        [MemoryPackable]
        public partial class JoinLobby : INetworkContext
        {
            public string Code { get; set; } = string.Empty;
        }

        // A place in the running match of the code, for a player in the lobby of its late players. Answered by
        // Result: Ok - the address of the match; NotFound - the match is over; Full - no free place; Started - the
        // match can't be entered now.
        [MemoryPackable]
        public partial class JoinMatch : INetworkContext
        {
            public string Code { get; set; } = string.Empty;
        }

        // The "Connect" window is open: from now on the meta pushes List to the user on every change of the open
        // lobbies, the current one first. A repeated Subscribe restarts the subscription.
        [MemoryPackable]
        public partial class Subscribe : INetworkContext
        {
        }

        // The window is closed: no more List pushes.
        [MemoryPackable]
        public partial class Unsubscribe : INetworkContext
        {
        }

        // Projection: every open lobby with a free place - lobbies that have not started and the matches they
        // have started (IsRunning). A lobby is listed while it is open, has a free place and can be entered.
        [MemoryPackable]
        public partial class List : INetworkContext
        {
            public List<Entry> Entries { get; set; } = new();
        }

        [MemoryPackable]
        public partial class Entry
        {
            public string Code { get; set; } = string.Empty;

            // The profile name of the leader of the lobby (the owner of the match).
            public string LeaderName { get; set; } = string.Empty;

            public int PlayersCount { get; set; }
            public int MaxPlayers { get; set; }

            // The lobby has started its match: an entry by the code leads to the lobby of the late players.
            public bool IsRunning { get; set; }
        }
    }
}
