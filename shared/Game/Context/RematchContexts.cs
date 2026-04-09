using System;
using MemoryPack;

namespace Shared
{
    public static partial class RematchContexts
    {
        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                   .Add<Request>()
                   .Add<Failure>()
                   .Add<Success>();
        }

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
}