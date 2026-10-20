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
        }
    }

    [MemoryPackUnion(32, typeof(RematchContexts.Request))]
    [MemoryPackUnion(33, typeof(RematchContexts.Failure))]
    [MemoryPackUnion(34, typeof(RematchContexts.Success))]
    public partial interface INetworkContext
    {
    }
}
