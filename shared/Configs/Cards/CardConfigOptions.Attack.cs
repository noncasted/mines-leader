using MemoryPack;

namespace Shared
{
    public partial class CardConfigOptions
    {
        [MemoryPackable]
        public partial class Trebuchet : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class OpponentFlagErase : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class OpponentFlagReshuffle : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class OpponentBomb : ICardConfig, IDamageCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 2;
            public int Damage { get; set; } = 1;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class Smoke : ICardConfig, IDurationalCardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int TurnsDuration { get; set; } = 3;
        }

        [MemoryPackable]
        public partial class ChainReaction : ICardConfig, IChainCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 4;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int MaxChain { get; set; } = 3;
            public int SearchRadius { get; set; } = 4;
            public int SpawnSize { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class FogOfWar : ICardConfig, IDurationalCardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int TurnsDuration { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class MineCluster : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 3;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class CarpetBomb : ICardConfig, ILengthCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 5;
            public int Length { get; set; } = 5;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class FortuneBlast : ICardConfig, IRandomSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int MinSize { get; set; } = 1;
            public int MaxSize { get; set; } = 4;
        }

        [MemoryPackable]
        public partial class ChaosFog : ICardConfig, IDurationalCardConfig, IRandomSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int MinSize { get; set; } = 1;
            public int MaxSize { get; set; } = 4;
            public int TurnsDuration { get; set; } = 3;
        }

        [MemoryPackable]
        public partial class Frost : ICardConfig, IDurationalCardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 2;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int TurnsDuration { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class Blackout : ICardConfig, IDurationalCardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 2;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int TurnsDuration { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class DimensionRift : ICardConfig, IAreaSizeCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Attack;
            public int ManaCost { get; set; } = 4;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }
    }
}
