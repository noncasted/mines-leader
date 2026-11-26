using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class ResponseMessageFromServer : IMessageFromServer
    {
        [MemoryPackAllowSerialize] public INetworkContext Context { get; set; }
        public int RequestId { get; set; }
    }
}
