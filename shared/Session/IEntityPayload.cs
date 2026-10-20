using MemoryPack;

namespace Shared
{
    // Union tags are declared next to the payload types, as on INetworkContext.
    [MemoryPackable]
    public partial interface IEntityPayload
    {
    }
}