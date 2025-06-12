using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class ServerEmptyRequest : IServerRequest
    {
        public INetworkContext Context { get; set; }
    }
}