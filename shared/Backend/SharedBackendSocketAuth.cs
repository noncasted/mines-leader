using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // S3: the authorization of the meta socket. The client sends Request as the first frame right after the socket
    // opens (nothing else is sent before Response); the server answers within its handshake timeout or closes the
    // socket. Nothing of it travels in the query of the upgrade: the query ends up in the logs of every proxy.
    // A refusal is Response with a Status other than Ok, then the socket is closed with RejectedCloseCode (a browser
    // WebSocket does not see the HTTP status of a refused upgrade, as with ProtocolVersion.ClientOutdatedCloseCode).
    public static partial class SharedBackendSocketAuth
    {
        public const int RejectedCloseCode = 4003;
        public const string RejectedReason = "Authorization rejected";

        // - GuestId + GuestSecret: a guest saved by this client (the secret was issued by the server once);
        // - nothing: a new guest.
        // A guest unknown to the server (a wiped database) gets a new guest. A bare user id is never enough: a user
        // without a secret (made before the secret existed) cannot be entered at all.
        [MemoryPackable]
        public partial class Request : INetworkContext
        {
            public Guid GuestId { get; set; }
            public string GuestSecret { get; set; } = string.Empty;
        }

        // Ok: UserId is the user of the connection. IssuedSecret is set only when the server has just issued the
        // secret of a guest (a new guest): the client saves UserId + IssuedSecret, it is never sent again.
        [MemoryPackable]
        public partial class Response : INetworkContext
        {
            public AuthStatus Status { get; set; }
            public Guid UserId { get; set; }
            public string IssuedSecret { get; set; } = string.Empty;
        }

        public enum AuthStatus
        {
            Ok = 0,

            // The guest secret does not match, or the guest has no secret at all (a user made before the secret).
            InvalidCredentials = 1,

            // A guest id without its secret or a secret without its id.
            InvalidRequest = 2,
        }
    }

    [MemoryPackUnion(76, typeof(SharedBackendSocketAuth.Request))]
    [MemoryPackUnion(77, typeof(SharedBackendSocketAuth.Response))]
    public partial interface INetworkContext
    {
    }
}
