using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class SharedAgentObservationRequest : INetworkContext
    {
    }

    [MemoryPackUnion(1046, typeof(SharedAgentObservationRequest))]
    public partial class NetworkContextUnion
    {
    }
}
