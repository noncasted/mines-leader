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
}
