using System;
using MemoryPack;

namespace Shared
{
    public partial class PlayerSnapshotRecord
    {
        [MemoryPackable]
        public partial class Health : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Value { get; set; }
        }
        
        [MemoryPackable]
        public partial class Mana : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Value { get; set; }
        }
    }
}