using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerManaState
    {
        public int Current { get; set; }
        public int Max { get; set; }
    }
}