using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class ServerEmptyResponse : IServerResponse
    {
        public INetworkContext Context { get; set; }
    }
}