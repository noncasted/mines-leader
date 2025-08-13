using System;
using MemoryPack;

namespace Shared
{
    public partial class PlayerSnapshotRecord
    {
        [MemoryPackable]
        public partial class Card : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public ICardActionData Data { get; set; }
            public CardType Type { get; set; }
            public int EntityId { get; set; }
        }

        [MemoryPackable]
        public partial class CardDraw : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public CardType Type { get; set; }
        }
    }
}