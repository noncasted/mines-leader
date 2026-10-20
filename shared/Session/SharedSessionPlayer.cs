using System;
using MemoryPack;

namespace Shared
{
    public static partial class SharedSessionPlayer
    {
        [MemoryPackable]
        public partial class LocalUpdate : INetworkContext
        {
            public int Index { get; set; }
        }

        [MemoryPackable]
        public partial class RemoteUpdate : INetworkContext
        {
            public int Index { get; set; }
            public Guid BackendId { get; set; }
        }

        [MemoryPackable]
        public partial class RemoteDisconnect : INetworkContext
        {
            public int Index { get; set; }
        }
    }

    [MemoryPackUnion(69, typeof(SharedSessionPlayer.LocalUpdate))]
    [MemoryPackUnion(70, typeof(SharedSessionPlayer.RemoteUpdate))]
    [MemoryPackUnion(71, typeof(SharedSessionPlayer.RemoteDisconnect))]
    public partial interface INetworkContext
    {
    }
}
