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
            public bool HasDropPosition { get; set; }
            public float DropX { get; set; }
            public float DropY { get; set; }
        }

        [MemoryPackable]
        public partial class CardAdd : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public CardType Type { get; set; }
            public Guid CardId { get; set; }
            public bool IsStash { get; set; }
        }

        /// <summary>
        /// The card leaves the hand. IsStash means the card does not just vanish,
        /// but flies to the stash beyond the screen edge (a discard from the hand, not a play).
        /// </summary>
        [MemoryPackable]
        public partial class CardRemove : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public Guid CardId { get; set; }
            public bool IsStash { get; set; }
        }

        [MemoryPackable]
        public partial class ManaUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Current { get; set; }
            public int BaseMax { get; set; }
            public int ResultMax { get; set; }
        }

        [MemoryPackable]
        public partial class HealthUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Current { get; set; }
            public int BaseMax { get; set; }
            public int ResultMax { get; set; }
        }

        [MemoryPackable]
        public partial class MovesUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Left { get; set; }
            public int BaseMax { get; set; }
            public int ResultMax { get; set; }
            public bool IsAvailable { get; set; }
        }

        [MemoryPackable]
        public partial class ModifierUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public DurationalModifierOverview Overview { get; set; } = new();
        }

        [MemoryPackable]
        public partial class DeckUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Count { get; set; }
        }

        /// <summary>
        /// Cards the player has played and which lie on the table move to the stash.
        /// The server decides the moment, until then the client keeps the cards in the dropped state.
        /// </summary>
        [MemoryPackable]
        public partial class CardsStashed : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
        }

        [MemoryPackable]
        public partial class StashUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Count { get; set; }
        }

        [MemoryPackable]
        public partial class BoardStateUpdate : IMoveSnapshotRecord
        {
            public Guid PlayerId { get; set; }
            public int Mines { get; set; }
            public int Flags { get; set; }
        }
    }
}