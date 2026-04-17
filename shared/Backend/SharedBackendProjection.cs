using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class SharedBackendProjection : INetworkContext
    {
        public INetworkContext Context { get; set; }
    }

    public static class SharedBackendProjectionExtensions
    {
        public static SharedBackendProjection ToProjection(this INetworkContext context)
        {
            return new SharedBackendProjection()
            {
                Context = context
            };
        }
    }
}