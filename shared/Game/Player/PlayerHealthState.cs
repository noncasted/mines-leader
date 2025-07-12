using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerHealthState
    {
        public int Current { get; set; }
        public int Max { get; set; }
    }
}