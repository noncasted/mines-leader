using System;
using MemoryPack;

namespace Shared
{
    public static partial class SharedSessionPlayer
    {
        // Id is the id of the user in this session only (a new one in every session): the meta id of a user is
        // its login and is never sent to the game clients (S3).
        [MemoryPackable]
        public partial class LocalUpdate : INetworkContext
        {
            public int Index { get; set; }
            public Guid Id { get; set; }
        }

        [MemoryPackable]
        public partial class RemoteUpdate : INetworkContext
        {
            public int Index { get; set; }
            public Guid Id { get; set; }
        }

        [MemoryPackable]
        public partial class RemoteDisconnect : INetworkContext
        {
            public int Index { get; set; }
        }
    }

    [MemoryPackUnion(40, typeof(SharedSessionPlayer.LocalUpdate))]
    [MemoryPackUnion(41, typeof(SharedSessionPlayer.RemoteUpdate))]
    [MemoryPackUnion(42, typeof(SharedSessionPlayer.RemoteDisconnect))]
    public partial class NetworkContextUnion
    {
    }
}
