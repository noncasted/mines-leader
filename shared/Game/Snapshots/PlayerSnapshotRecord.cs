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
            public bool IsStash { get; set; }
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