using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // Direct connections between the players of a session (WebRTC data channels). The backend only relays the
    // negotiation: a signal goes from its sender to the user of TargetIndex, stamped with the index of the sender.
    // A guest that gets no direct connection to the host of a P2P match plays through the backend: its frames to the
    // host and back go as Tunnel, relayed the same way and never read by the backend.
    public static partial class SharedSessionPeer
    {
        public enum SignalKind : byte
        {
            // Type: "offer" / "answer", Value: the SDP.
            Description = 0,

            // Value: the candidate line, Mid: the media id of the candidate.
            Candidate = 1
        }

        [MemoryPackable]
        public partial class Signal : INetworkContext
        {
            // Written by the sender: the user the signal is for.
            public int TargetIndex { get; set; }

            // Written by the backend.
            public int SenderIndex { get; set; }

            public SignalKind Kind { get; set; }
            public string Type { get; set; }
            public string Value { get; set; }
            public string Mid { get; set; }
        }

        [MemoryPackable]
        public partial class Tunnel : INetworkContext
        {
            // Written by the sender: the user the frame is for.
            public int TargetIndex { get; set; }

            // Written by the backend.
            public int SenderIndex { get; set; }

            // A whole frame of the session of the host (a message of the client or of the host), as its socket carries
            // it.
            public byte[] Frame { get; set; }
        }
    }
}
