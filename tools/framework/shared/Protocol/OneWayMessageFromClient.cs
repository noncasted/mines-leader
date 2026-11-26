using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class OneWayMessageFromClient : IMessageFromClient
    {
        [MemoryPackAllowSerialize] public INetworkContext Context { get; set; }
    }
}
