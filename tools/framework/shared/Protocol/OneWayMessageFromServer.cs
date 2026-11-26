using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class OneWayMessageFromServer : IMessageFromServer
    {
        [MemoryPackAllowSerialize] public INetworkContext Context { get; set; }
    }
}
