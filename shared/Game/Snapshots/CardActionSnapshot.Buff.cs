#nullable enable annotations
using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class Gravedigger : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Overclock : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Scavenger : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class ManaSurge : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Adrenaline : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class BloodPact : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class CoinToss : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
        }

        [MemoryPackable]
        public partial class ManaFountain : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public int RolledAmount { get; set; }
        }

        [MemoryPackable]
        public partial class Focus : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class PowerSurge : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Recycler : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class MysticDraw : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
        }

        [MemoryPackable]
        public partial class DoubleOrNothing : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
            public int ResultMana { get; set; }
        }

        [MemoryPackable]
        public partial class GamblersRuin : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
        }

        [MemoryPackable]
        public partial class Salvage : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<CardType> PeekedCards { get; set; }
            public int ChosenIndex { get; set; }
        }

        [MemoryPackable]
        public partial class MirrorMatch : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public CardType CopiedCard { get; set; }
            public ICardActionData? CopiedAction { get; set; }
        }
    }
}
