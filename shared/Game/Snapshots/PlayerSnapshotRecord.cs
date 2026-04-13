using System;
using MemoryPack;

namespace Shared
{
    public partial class PlayerSnapshotRecord
    {
        [MemoryPackable]
        public partial class CardUse : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public ICardActionData Data { get; set; }
            public Guid CardId { get; set; }
        }

        [MemoryPackable]
        public partial class CardAdd : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public CardType Type { get; set; }
            public Guid CardId { get; set; }
        }

        [MemoryPackable]
        public partial class CardRemove : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public Guid CardId { get; set; }
        }

        [MemoryPackable]
        public partial class ManaUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Current { get; set; }
            public int Max { get; set; }
        }

        [MemoryPackable]
        public partial class HealthUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Current { get; set; }
            public int Max { get; set; }
        }

        [MemoryPackable]
        public partial class MovesUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Left { get; set; }
            public int Max { get; set; }
            public bool IsAvailable { get; set; }
        }
    }
}