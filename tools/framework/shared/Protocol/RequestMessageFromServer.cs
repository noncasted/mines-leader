using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class RequestMessageFromServer : IMessageFromServer
    {
        [MemoryPackAllowSerialize] public INetworkContext Context { get; set; }
        public int RequestId { get; set; }
    }
}
