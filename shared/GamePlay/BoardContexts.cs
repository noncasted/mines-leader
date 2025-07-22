using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public partial class BoardContexts
    {
        [MemoryPackable]
        public partial class Open
        {
            public Guid BoardOwner { get; set; }
            public IReadOnlyList<BoardCellContexts.Taken> Exploded { get; set; }
            public IReadOnlyList<BoardCellContexts.Free> Freed { get; set; }
        }
    
        [MemoryPackable]
        public partial class Flag
        {
            public Guid BoardOwner { get; set; }
            public IReadOnlyList<BoardCellContexts.Taken> Flagged { get; set; }
        }
    
        [MemoryPackable]
        public partial class Card
        {
            public Guid BoardOwner { get; set; }
        }
    }
}

