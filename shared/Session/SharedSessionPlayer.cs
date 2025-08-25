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
        
        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                .Add<LocalUpdate>()
                .Add<RemoteUpdate>()
                .Add<RemoteDisconnect>();
        }
    }
}