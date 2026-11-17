using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class EmptyStatePayload : IStatePayload
    {
    }

    public static class StatePayloadTags
    {
        public const ushort Empty = 0;
    }
}
