using System;
using MemoryPack;

namespace Shared
{
    public partial class SharedBackendSocketAuth
    {
        [MemoryPackable]
        public partial class Request : INetworkContext
        {
            public Guid UserId { get; set; }
        }

        [MemoryPackable]
        public partial class Response : INetworkContext
        {
            public bool IsSuccess { get; set; }
        }
    }
}