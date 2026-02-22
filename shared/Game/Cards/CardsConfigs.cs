using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardsConfigs.Bloodhound))]
    [MemoryPackUnion(1, typeof(CardsConfigs.Trebuchet))]
    [MemoryPackUnion(2, typeof(CardsConfigs.TrebuchetAimer))]
    [MemoryPackUnion(3, typeof(CardsConfigs.ErosionDozer))]
    [MemoryPackUnion(4, typeof(CardsConfigs.Gravedigger))]
    [MemoryPackUnion(5, typeof(CardsConfigs.ZipZap))]
    [MemoryPackUnion(6, typeof(CardsConfigs.OpponentFlagErase))]
    [MemoryPackUnion(7, typeof(CardsConfigs.OpponentFlagReshuffle))]
    [MemoryPackUnion(8, typeof(CardsConfigs.OpponentBomb))]
    [MemoryPackUnion(9, typeof(CardsConfigs.Smoke))]
    public partial interface ICardConfig
    {
        CardType Type { get; set; }
        int ManaCost { get; set; }
        CardTarget Target { get; }
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardsConfigs.Bloodhound))]
    [MemoryPackUnion(1, typeof(CardsConfigs.Trebuchet))]
    [MemoryPackUnion(2, typeof(CardsConfigs.TrebuchetAimer))]
    [MemoryPackUnion(3, typeof(CardsConfigs.ErosionDozer))]
    [MemoryPackUnion(4, typeof(CardsConfigs.ZipZap))]
    [MemoryPackUnion(5, typeof(CardsConfigs.OpponentFlagErase))]
    [MemoryPackUnion(6, typeof(CardsConfigs.OpponentFlagReshuffle))]
    [MemoryPackUnion(7, typeof(CardsConfigs.Smoke))]
    public partial interface ICardSizeConfig
    {
        int Size { get; set; }
    }

    [MemoryPackable]
    public partial class CardsConfigs : INetworkContext
    {
        public Bloodhound BloodHound_Normal { get; } = new();
        public Bloodhound BloodHound_Max { get; } = new();

        public Trebuchet Trebuchet_Normal { get; } = new();
        public Trebuchet Trebuchet_Max { get; } = new();

        public TrebuchetAimer TrebuchetAimer_Normal { get; } = new();
        public TrebuchetAimer TrebuchetAimer_Max { get; } = new();

        public ErosionDozer ErosionDozer_Normal { get; } = new();
        public ErosionDozer ErosionDozer_Max { get; } = new();

        public Gravedigger Gravedigger_Normal { get; } = new();

        public ZipZap ZipZap_Normal { get; } = new();
        public ZipZap ZipZap_Max { get; } = new();

        public OpponentFlagErase OpponentFlagErase_Normal { get; } = new();
        public OpponentFlagErase OpponentFlagErase_Max { get; } = new();

        public OpponentBomb OpponentBomb_Normal { get; } = new();

        public OpponentFlagReshuffle OpponentFlagReshuffle_Normal { get; } = new();
        public OpponentFlagReshuffle OpponentFlagReshuffle_Max { get; } = new();

        public Smoke Smoke_Normal { get; } = new();
        public Smoke Smoke_Max { get; } = new();

        public IReadOnlyDictionary<CardType, ICardConfig> All => new Dictionary<CardType, ICardConfig>()
        {
            { CardType.Bloodhound, BloodHound_Normal },
            { CardType.Bloodhound_Max, BloodHound_Max },

            { CardType.Trebuchet, Trebuchet_Normal },
            { CardType.Trebuchet_Max, Trebuchet_Max },

            { CardType.TrebuchetAimer, TrebuchetAimer_Normal },
            { CardType.TrebuchetAimer_Max, TrebuchetAimer_Max },

            { CardType.ErosionDozer, ErosionDozer_Normal },
            { CardType.ErosionDozer_Max, ErosionDozer_Max },

            { CardType.ZipZap, ZipZap_Normal },
            { CardType.ZipZap_Max, ZipZap_Max },

            { CardType.OpponentFlagErase, OpponentFlagErase_Normal },
            { CardType.OpponentFlagErase_Max, OpponentFlagErase_Normal },

            { CardType.OpponentFlagReshuffle, OpponentFlagReshuffle_Normal },
            { CardType.OpponentFlagReshuffle_Max, OpponentFlagReshuffle_Normal },

            { CardType.Smoke, Smoke_Normal },
            { CardType.Smoke_Max, Smoke_Max },

            { CardType.OpponentBomb, OpponentBomb_Normal },
            { CardType.Gravedigger, Gravedigger_Normal },
        };

        [MemoryPackable]
        public partial class Bloodhound : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Trebuchet : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 1;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ErosionDozer : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 5;
            public int ManaCost { get; set; } = 4;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Gravedigger : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 4;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class ZipZap : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
            public int SearchRadius => 4;
        }

        [MemoryPackable]
        public partial class OpponentFlagErase : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class OpponentFlagReshuffle : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class OpponentBomb : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class Smoke : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int Duration => 3;
        }
    }
}