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
    }
}