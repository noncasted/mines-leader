using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class ServerFullResponse : IServerResponse
    {
        public INetworkContext Context { get; set; }
        public int RequestId { get; set; }
    }
}