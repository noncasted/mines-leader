using MemoryPack;

namespace Shared
{
    public partial class MatchActionContexts
    {
        [MemoryPackable]
        public partial class PlayerReady : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class PlayerLoaded : INetworkContext
        {
        }
    }

    [MemoryPackUnion(1043, typeof(MatchActionContexts.PlayerReady))]
    [MemoryPackUnion(1044, typeof(MatchActionContexts.PlayerLoaded))]
    public partial class NetworkContextUnion
    {
    }
}
