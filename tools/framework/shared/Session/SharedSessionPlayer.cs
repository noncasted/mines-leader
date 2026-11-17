using System;
using MemoryPack;

namespace Shared
{
    public static partial class SharedSessionPlayer
    {
        // The first message of a connection: the user index in the session (its seat, kept across a reconnect).
        // Id is the id of the user in this session only (a new one in every session): the meta id of a user is its
        // login and is never sent to the game clients.
        [MemoryPackable]
        public partial class LocalUpdate : INetworkContext
        {
            public int Index { get; set; }
            public Guid Id { get; set; }

            // The last entity counter used in the id range of the seat (SharedEntityIds): a returning client goes on
            // after it, ids are never reused. 0 for a new seat.
            public int EntityCounter { get; set; }

            // The seat existed before this connection (a reconnect): the entities of the user are alive in the
            // session and come in CreatedOverview with the local index as the owner - they are restored, not created.
            public bool IsReturn { get; set; }

            // The live entities of the seat (owned by the user, not the session ones), all sent in CreatedOverview
            // right after this message: a returning client knows when it has received every entity to restore.
            public int SeatEntities { get; set; }
        }

        // A user joined, or returned to its seat after RemoteAway.
        [MemoryPackable]
        public partial class RemoteUpdate : INetworkContext
        {
            public int Index { get; set; }
            public Guid Id { get; set; }
        }

        // A user left the session (Leave, the end of its reconnect window, a lobby connection closed): its entities
        // are destroyed with it.
        [MemoryPackable]
        public partial class RemoteDisconnect : INetworkContext
        {
            public int Index { get; set; }
        }

        // The connection of a user of a match is lost: its seat and entities wait for its return for the reconnect
        // window; then RemoteUpdate (returned) or RemoteDisconnect (the window expired).
        [MemoryPackable]
        public partial class RemoteAway : INetworkContext
        {
            public int Index { get; set; }
        }

        // Client -> server, one way, right before the client closes the connection on purpose (leaves the match):
        // the end of this connection is a leave, not a loss - no reconnect window.
        [MemoryPackable]
        public partial class Leave : INetworkContext
        {
        }
    }
}
