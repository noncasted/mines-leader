using System;
using MemoryPack;

namespace Shared
{
    public partial class CardUsePayload
    {
        [MemoryPackable]
        public partial class Gravedigger : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Overclock : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Scavenger : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class ManaSurge : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Adrenaline : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class BloodPact : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class CoinToss : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class ManaFountain : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Focus : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class PowerSurge : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Recycler : ICardUsePayload
        {
            public CardType Type { get; set; }
            public Guid DiscardCardId { get; set; }
        }

        [MemoryPackable]
        public partial class MysticDraw : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class DoubleOrNothing : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class GamblersRuin : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Salvage : ICardUsePayload
        {
            public CardType Type { get; set; }
            public int ChosenIndex { get; set; }
        }

        [MemoryPackable]
        public partial class MirrorMatch : ICardUsePayload
        {
            public CardType Type { get; set; }
        }
    }
}
