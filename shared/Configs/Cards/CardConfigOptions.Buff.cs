using MemoryPack;

namespace Shared
{
    public partial class CardConfigOptions
    {
        [MemoryPackable]
        public partial class TrebuchetAimer : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int Size { get; set; } = 1;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Gravedigger : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 4;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class Overclock : ICardConfig, IExtraMovesCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Self;
            public int ExtraMoves { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class Scavenger : ICardConfig, IDrawCountCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int DrawCount { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class ManaSurge : ICardConfig, IManaGainCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int ManaGain { get; set; } = 3;
        }

        [MemoryPackable]
        public partial class Adrenaline : ICardConfig, IExtraMovesCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int ExtraMoves { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class BloodPact : ICardConfig, IHpCostCardConfig, IManaGainCardConfig, IExtraMovesCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 0;
            public CardTarget Target => CardTarget.Self;
            public int HpCost { get; set; } = 1;
            public int ManaGain { get; set; } = 3;
            public int ExtraMoves { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class CoinToss : ICardConfig, ICoinTossCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int WinMoves { get; set; } = 2;
            public int LoseMoves { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class ManaFountain : ICardConfig, IManaRangeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int MinMana { get; set; } = 1;
            public int MaxMana { get; set; } = 5;
        }

        [MemoryPackable]
        public partial class Focus : ICardConfig, IDiscountCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int Discount { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class PowerSurge : ICardConfig, IDiscountCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Self;
            public int Discount { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class Recycler : ICardConfig, IDrawCountCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int DrawCount { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class MysticDraw : ICardConfig, IWinLoseDrawCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int WinDraw { get; set; } = 2;
            public int LoseReturn { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class DoubleOrNothing : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class GamblersRuin : ICardConfig, IGamblersRuinCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int WinDraw { get; set; } = 3;
            public int WinMana { get; set; } = 2;
            public int LoseDiscard { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class Salvage : ICardConfig, IPeekCountCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int PeekCount { get; set; } = 3;
        }

        [MemoryPackable]
        public partial class MirrorMatch : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Buff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Self;
        }
    }
}
