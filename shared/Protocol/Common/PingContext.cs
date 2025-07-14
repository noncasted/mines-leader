using MemoryPack;

namespace Shared
{
    public partial class PingContext
    {
        [MemoryPackable]
        public partial class Request : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class Response : INetworkContext
        {
        }

        [MemoryPackIgnore] public static Request DefaultRequest { get; } = new();
        [MemoryPackIgnore] public static Response DefaultResponse { get; } = new();
    }
}