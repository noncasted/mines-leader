using System;
using MemoryPack;

namespace Shared
{
    public static partial class UserContexts
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
}