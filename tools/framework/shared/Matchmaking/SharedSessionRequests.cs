using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // The entry of a user into a game session, asked from the meta: a public session with a free place, a private one
    // for the user alone, a ticket into a session the user already holds a place in. The lobby entries
    // (SharedLobbies) are answered the same way. Union tags: NetworkContextTags.Session* (the project lists them).
    public static partial class SharedSessionRequests
    {
        // A public match session with a free place, or the lobby of the late players of a running one (Result.IsLobby).
        [MemoryPackable]
        public partial class Search : INetworkContext
        {
        }

        // A private match session for the caller alone, without a lobby (a solo run, a tutorial).
        [MemoryPackable]
        public partial class CreateSolo : INetworkContext
        {
        }

        // The answer to every entry: Search, CreateSolo, SharedLobbies.CreateLobby / JoinLobby / JoinMatch.
        // ServerUrl, SessionId and Ticket are set only when Status is Ok, Code - only for lobby sessions.
        // Ticket: the entry into the session for this user (SharedSessionAuth.Request), valid for a minute, once.
        [MemoryPackable]
        public partial class Result : INetworkContext
        {
            public string ServerUrl { get; set; } = string.Empty;
            public Guid SessionId { get; set; }
            public string Code { get; set; } = string.Empty;
            public EntryStatus Status { get; set; }
            public string Ticket { get; set; } = string.Empty;

            // Search only: the session is the lobby of the late players of a running match (the user enters the match
            // from it, see LobbyState.IsRunning), not the match itself.
            public bool IsLobby { get; set; }
        }

        public enum EntryStatus
        {
            Ok = 0,
            NotFound = 1,
            Full = 2,

            // The lobby of the code has started its match and the match can't be entered now.
            Started = 3,
        }

        // A new ticket into a session the user already holds a place in, which it got not from a Result: the match a
        // lobby has started (LobbyGameState). The meta does not check the place: the game server does, on the entry
        // (an entry without a live reservation is refused).
        [MemoryPackable]
        public partial class TicketRequest : INetworkContext
        {
            public Guid SessionId { get; set; }
        }

        [MemoryPackable]
        public partial class TicketResponse : INetworkContext
        {
            public string Ticket { get; set; } = string.Empty;
        }
    }
}
