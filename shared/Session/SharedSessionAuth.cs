using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public partial class SharedSessionAuth
    {
        [MemoryPackable]
        public partial class Request : INetworkContext
        {
            public Guid UserId { get; set; }
            public Guid SessionId { get; set; }
        }

        [MemoryPackable]
        public partial class Response : INetworkContext
        {
            public bool IsSuccess { get; set; }
        }
    }

    [MemoryPackUnion(59, typeof(SharedSessionAuth.Request))]
    [MemoryPackUnion(60, typeof(SharedSessionAuth.Response))]
    public partial interface INetworkContext
    {
    }
}
