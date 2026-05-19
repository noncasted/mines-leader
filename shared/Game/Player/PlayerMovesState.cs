using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerMovesState
    {
        public int Left { get; set; }
        public int BaseMax { get; set; }
        public int ResultMax { get; set; }
        public bool IsAvailable { get; set; }
    }
}