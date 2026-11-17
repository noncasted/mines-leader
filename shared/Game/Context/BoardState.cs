using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class BoardState
    {
        public int Mines { get; set; }
        public int Flags { get; set; }
    }
}