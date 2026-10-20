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

    [MemoryPackUnion(53, typeof(MatchActionContexts.PlayerReady))]
    [MemoryPackUnion(54, typeof(MatchActionContexts.PlayerLoaded))]
    public partial interface INetworkContext
    {
    }
}
