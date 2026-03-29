using System.Collections.Generic;
using MemoryPack;
using Newtonsoft.Json;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardConfigOptions.Bloodhound))]
    [MemoryPackUnion(1, typeof(CardConfigOptions.Trebuchet))]
    [MemoryPackUnion(2, typeof(CardConfigOptions.TrebuchetAimer))]
    [MemoryPackUnion(3, typeof(CardConfigOptions.ErosionDozer))]
    [MemoryPackUnion(4, typeof(CardConfigOptions.Gravedigger))]
    [MemoryPackUnion(5, typeof(CardConfigOptions.ZipZap))]
    [MemoryPackUnion(6, typeof(CardConfigOptions.OpponentFlagErase))]
    [MemoryPackUnion(7, typeof(CardConfigOptions.OpponentFlagReshuffle))]
    [MemoryPackUnion(8, typeof(CardConfigOptions.OpponentBomb))]
    [MemoryPackUnion(9, typeof(CardConfigOptions.Smoke))]
    [MemoryPackUnion(10, typeof(CardConfigOptions.Medic))]
    [MemoryPackUnion(11, typeof(CardConfigOptions.MinefieldScout))]
    [MemoryPackUnion(13, typeof(CardConfigOptions.Siphon))]
    [MemoryPackUnion(16, typeof(CardConfigOptions.ChainReaction))]
    [MemoryPackUnion(17, typeof(CardConfigOptions.Overclock))]
    [MemoryPackUnion(18, typeof(CardConfigOptions.FogOfWar))]
    [MemoryPackUnion(19, typeof(CardConfigOptions.Scavenger))]
    [MemoryPackUnion(20, typeof(CardConfigOptions.HandScramble))]
    public partial interface ICardConfig
    {
        CardType Type { get; set; }
        int ManaCost { get; set; }
        CardTarget Target { get; }
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardConfigOptions.Bloodhound))]
    [MemoryPackUnion(1, typeof(CardConfigOptions.Trebuchet))]
    [MemoryPackUnion(2, typeof(CardConfigOptions.TrebuchetAimer))]
    [MemoryPackUnion(3, typeof(CardConfigOptions.ErosionDozer))]
    [MemoryPackUnion(4, typeof(CardConfigOptions.ZipZap))]
    [MemoryPackUnion(5, typeof(CardConfigOptions.OpponentFlagErase))]
    [MemoryPackUnion(6, typeof(CardConfigOptions.OpponentFlagReshuffle))]
    [MemoryPackUnion(7, typeof(CardConfigOptions.Smoke))]
    [MemoryPackUnion(8, typeof(CardConfigOptions.MinefieldScout))]
    [MemoryPackUnion(11, typeof(CardConfigOptions.FogOfWar))]
    public partial interface ICardSizeConfig
    {
        int Size { get; set; }
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardConfigOptions.Siphon))]
    public partial interface ICardDrainConfig
    {
        int DrainAmount { get; set; }
    }

    [MemoryPackable]
    public partial class CardConfigOptions : INetworkContext
    {
        public Bloodhound BloodHound_Normal { get; set; } = new();
        public Bloodhound BloodHound_Max { get; set; } = new();

        public Trebuchet Trebuchet_Normal { get; set; } = new();
        public Trebuchet Trebuchet_Max { get; set; } = new();

        public TrebuchetAimer TrebuchetAimer_Normal { get; set; } = new();
        public TrebuchetAimer TrebuchetAimer_Max { get; set; } = new();

        public ErosionDozer ErosionDozer_Normal { get; set; } = new();
        public ErosionDozer ErosionDozer_Max { get; set; } = new();

        public Gravedigger Gravedigger_Normal { get; set; } = new();

        public ZipZap ZipZap_Normal { get; set; } = new();
        public ZipZap ZipZap_Max { get; set; } = new();

        public OpponentFlagErase OpponentFlagErase_Normal { get; set; } = new();
        public OpponentFlagErase OpponentFlagErase_Max { get; set; } = new();

        public OpponentBomb OpponentBomb_Normal { get; set; } = new();

        public OpponentFlagReshuffle OpponentFlagReshuffle_Normal { get; set; } = new();
        public OpponentFlagReshuffle OpponentFlagReshuffle_Max { get; set; } = new();

        public Smoke Smoke_Normal { get; set; } = new();
        public Smoke Smoke_Max { get; set; } = new();

        public Medic Medic_Normal { get; set; } = new();

        public MinefieldScout MinefieldScout_Normal { get; set; } = new();
        public MinefieldScout MinefieldScout_Max { get; set; } = new();

        public Siphon Siphon_Normal { get; set; } = new();

        public ChainReaction ChainReaction_Normal { get; set; } = new();

        public Overclock Overclock_Normal { get; set; } = new();

        public FogOfWar FogOfWar_Normal { get; set; } = new();
        public FogOfWar FogOfWar_Max { get; set; } = new();

        public Scavenger Scavenger_Normal { get; set; } = new();

        public HandScramble HandScramble_Normal { get; set; } = new();

        [JsonIgnore]
        [MemoryPackIgnore]
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

            { CardType.Medic, Medic_Normal },

            { CardType.MinefieldScout, MinefieldScout_Normal },
            { CardType.MinefieldScout_Max, MinefieldScout_Max },

            { CardType.Siphon, Siphon_Normal },

            { CardType.ChainReaction, ChainReaction_Normal },

            { CardType.Overclock, Overclock_Normal },

            { CardType.FogOfWar, FogOfWar_Normal },
            { CardType.FogOfWar_Max, FogOfWar_Max },

            { CardType.Scavenger, Scavenger_Normal },

            { CardType.HandScramble, HandScramble_Normal },
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

        [MemoryPackable]
        public partial class Medic : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 4;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class MinefieldScout : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 5;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Siphon : ICardConfig, ICardDrainConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public int DrainAmount { get; set; } = 1;
            public CardTarget Target => CardTarget.Opponent;
        }

        [MemoryPackable]
        public partial class ChainReaction : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 4;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int MaxChain => 3;
            public int SpawnSize => 2;
        }

        [MemoryPackable]
        public partial class Overclock : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Self;
            public int ExtraMoves => 2;
        }

        [MemoryPackable]
        public partial class FogOfWar : ICardConfig, ICardSizeConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int Duration => 2;
        }

        [MemoryPackable]
        public partial class Scavenger : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int DrawCount => 2;
        }

        [MemoryPackable]
        public partial class HandScramble : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
        }
    }
}