using System;
using MemoryPack;

namespace Shared
{
    public static partial class RematchContexts
    {
        [MemoryPackable]
        public partial class Request : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class Failure : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class Success : INetworkContext
        {
            public Guid SessionId { get; set; }
            public string ServerUrl { get; set; }

            // S4: the entry of this user into the new session, issued by the game server (one per user).
            public string Ticket { get; set; }
        }
    }

    [MemoryPackUnion(1022, typeof(RematchContexts.Request))]
    [MemoryPackUnion(1023, typeof(RematchContexts.Failure))]
    [MemoryPackUnion(1024, typeof(RematchContexts.Success))]
    public partial class NetworkContextUnion
    {
    }
}
