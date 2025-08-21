using MemoryPack;

namespace Shared
{
    public partial class MatchActionContexts
    {
        [MemoryPackable]
        public partial class PlayerReady : INetworkContext
        {
        }
    }
}