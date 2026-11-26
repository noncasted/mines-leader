using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class ResponseMessageFromClient : IMessageFromClient
    {
        [MemoryPackAllowSerialize] public INetworkContext Context { get; set; }
        public int RequestId { get; set; }
    }
}
