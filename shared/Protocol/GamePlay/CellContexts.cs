using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class BoardPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public partial class BoardCellContexts
    {
        [MemoryPackable]
        public partial class State
        {
            public BoardPosition Position { get; set; }
        }

        [MemoryPackable]
        public partial class Free : State
        {
            public int MinesAround { get; set; }
        }
    
        [MemoryPackable]
        public partial class Taken : State
        {
            public bool IsFlagged { get; set; }
        }
    }
}