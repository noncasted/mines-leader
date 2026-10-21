using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // S4: the first frame of a game socket. The ticket is issued by the meta (SharedMatchmaking.MatchResult.Ticket,
    // SharedMatchmaking.LobbyResult.Ticket) or by the game server for a rematch (RematchContexts.Success.Ticket) and is
    // opaque to the client: it names the user and the session, expires in a minute and is accepted once. The game
    // server answers within its handshake timeout or closes the socket.
    public partial class SharedSessionAuth
    {
        [MemoryPackable]
        public partial class Request : INetworkContext
        {
            public string Ticket { get; set; } = string.Empty;
        }

        [MemoryPackable]
        public partial class Response : INetworkContext
        {
            public SessionAuthStatus Status { get; set; }
        }
    }

    public enum SessionAuthStatus
    {
        Ok = 0,

        // Not signed by the meta, malformed, or already used.
        InvalidTicket = 1,

        Expired = 2,

        // The user holds no live place in the session (never reserved, expired, already used by another connection).
        NoReservation = 3,

        // The session is over or unknown on this server.
        Closed = 4,
    }

    [MemoryPackUnion(59, typeof(SharedSessionAuth.Request))]
    [MemoryPackUnion(60, typeof(SharedSessionAuth.Response))]
    public partial interface INetworkContext
    {
    }
}
