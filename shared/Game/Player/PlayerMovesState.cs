using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerMovesState
    {
        public int Left { get; set; }
        public int Max { get; set; }
    }
}