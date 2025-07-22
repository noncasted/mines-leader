using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class SharedBackendProjection : INetworkContext
    {
        public INetworkContext Context { get; set; }
    }
}