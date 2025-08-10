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
        
        [MemoryPackable]
        public partial class Card : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class ReshuffleFromStash : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int CardsCount { get; set; }
        }
        
        [MemoryPackable]
        public partial class CardDraw : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class DeckFill : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Count { get; set; }
        }
    }
}