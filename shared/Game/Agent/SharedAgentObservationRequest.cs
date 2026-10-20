using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class SharedAgentObservationRequest : INetworkContext
    {
    }

    [MemoryPackUnion(56, typeof(SharedAgentObservationRequest))]
    public partial interface INetworkContext
    {
    }
}
