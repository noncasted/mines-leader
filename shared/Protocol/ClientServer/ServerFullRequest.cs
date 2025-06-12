using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class ServerFullRequest : IServerRequest
    {
        public INetworkContext Context { get; set; }
        public int RequestId { get; set; }
    }
}