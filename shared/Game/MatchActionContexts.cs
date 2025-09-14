using System;
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
        public partial class RequestRematch : INetworkContext
        {
            public bool IsConfirmed { get; set; }
            public Guid SessionId { get; set; }
            public string ServerUrl { get; set; }
        }
    }
}