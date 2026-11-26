using MemoryPack;

namespace Shared
{
    public partial class CardConfigOptions
    {
        [MemoryPackable]
        public partial class Bloodhound : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ErosionDozer : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int Size { get; set; } = 5;
            public int ManaCost { get; set; } = 4;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ZipZap : ICardConfig, IAreaSizeCardConfig, ISearchRadiusCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
            public int SearchRadius { get; set; } = 4;
        }

        [MemoryPackable]
        public partial class MinefieldScout : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int Size { get; set; } = 5;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Sonar : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Excavator : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int ManaCost { get; set; } = 3;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ThermalVision : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int ManaCost { get; set; } = 3;
            public int Size { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ChaosDiamond : ICardConfig, IRandomSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
            public int MinSize { get; set; } = 2;
            public int MaxSize { get; set; } = 5;
        }

        [MemoryPackable]
        public partial class ChaosScout : ICardConfig, IRandomLengthCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
            public int MinLength { get; set; } = 3;
            public int MaxLength { get; set; } = 7;
        }

        [MemoryPackable]
        public partial class FortuneCookie : ICardConfig, IMinesRangeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Scout;
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int MinMines { get; set; } = 1;
            public int MaxMines { get; set; } = 3;
        }
    }
}
