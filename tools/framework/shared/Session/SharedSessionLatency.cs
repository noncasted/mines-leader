using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // Latency probes of a session, for the network overlay of the client.
    // Ping: client -> backend -> the same client, the round trip to the backend.
    // Peer probe: client A -> backend -> every other client B -> backend -> A. The backend stamps the probe with its
    // own clock on the way to B and measures the B leg when the echo comes back, so A splits the whole loop into its
    // own leg and the leg of B without synchronized clocks.
    public static partial class SharedSessionLatency
    {
        [MemoryPackable]
        public partial class Ping : INetworkContext
        {
            public int Id { get; set; }
        }

        [MemoryPackable]
        public partial class Pong : INetworkContext
        {
            public int Id { get; set; }
        }

        [MemoryPackable]
        public partial class PeerProbe : INetworkContext
        {
            public int Id { get; set; }

            // Written by the backend.
            public int SenderIndex { get; set; }
            public long ServerTimestamp { get; set; }
        }

        [MemoryPackable]
        public partial class PeerEcho : INetworkContext
        {
            // Copied from the probe by the responder.
            public int Id { get; set; }
            public int SenderIndex { get; set; }
            public long ServerTimestamp { get; set; }

            // Ping of the responder to the backend.
            public float ResponderPingMs { get; set; }

            // Written by the backend: backend -> responder -> backend.
            public int ResponderIndex { get; set; }
            public float ResponderLegMs { get; set; }
        }
    }
}
