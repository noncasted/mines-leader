using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class BackendProjectionContext : INetworkContext
    {
        public INetworkContext Context { get; set; }
    }
}