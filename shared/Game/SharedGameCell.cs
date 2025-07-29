using MemoryPack;
using UnityEngine;

namespace Shared
{
    public partial class BoardCellContexts
    {
        [MemoryPackable]
        public partial class State
        {
            public Vector2Int Position { get; set; }
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