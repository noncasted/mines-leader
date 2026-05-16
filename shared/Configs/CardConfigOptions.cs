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
    [MemoryPackUnion(21, typeof(CardConfigOptions.Lockdown))]
    [MemoryPackUnion(23, typeof(CardConfigOptions.Sonar))]
    [MemoryPackUnion(24, typeof(CardConfigOptions.Purge))]
    [MemoryPackUnion(25, typeof(CardConfigOptions.Adrenaline))]
    [MemoryPackUnion(26, typeof(CardConfigOptions.ManaSurge))]
    [MemoryPackUnion(27, typeof(CardConfigOptions.BloodPact))]
    [MemoryPackUnion(28, typeof(CardConfigOptions.CoinToss))]
    [MemoryPackUnion(29, typeof(CardConfigOptions.ManaFountain))]
    [MemoryPackUnion(30, typeof(CardConfigOptions.Focus))]
    [MemoryPackUnion(31, typeof(CardConfigOptions.Shield))]
    [MemoryPackUnion(32, typeof(CardConfigOptions.PowerSurge))]
    [MemoryPackUnion(33, typeof(CardConfigOptions.Embargo))]
    [MemoryPackUnion(34, typeof(CardConfigOptions.Recycler))]
    [MemoryPackUnion(35, typeof(CardConfigOptions.MysticDraw))]
    [MemoryPackUnion(36, typeof(CardConfigOptions.DoubleOrNothing))]
    [MemoryPackUnion(37, typeof(CardConfigOptions.GamblersRuin))]
    [MemoryPackUnion(38, typeof(CardConfigOptions.Excavator))]
    [MemoryPackUnion(39, typeof(CardConfigOptions.ThermalVision))]
    [MemoryPackUnion(40, typeof(CardConfigOptions.ChaosDiamond))]
    [MemoryPackUnion(41, typeof(CardConfigOptions.ChaosScout))]
    [MemoryPackUnion(42, typeof(CardConfigOptions.MineCluster))]
    [MemoryPackUnion(43, typeof(CardConfigOptions.CarpetBomb))]
    [MemoryPackUnion(44, typeof(CardConfigOptions.FortuneBlast))]
    [MemoryPackUnion(45, typeof(CardConfigOptions.ChaosFog))]
    [MemoryPackUnion(46, typeof(CardConfigOptions.Frost))]
    [MemoryPackUnion(47, typeof(CardConfigOptions.Blackout))]
    [MemoryPackUnion(48, typeof(CardConfigOptions.FortuneCookie))]
    [MemoryPackUnion(49, typeof(CardConfigOptions.Salvage))]
    [MemoryPackUnion(50, typeof(CardConfigOptions.CardThief))]
    [MemoryPackUnion(51, typeof(CardConfigOptions.SabotageDeck))]
    [MemoryPackUnion(52, typeof(CardConfigOptions.Dud))]
    [MemoryPackUnion(53, typeof(CardConfigOptions.SoulLink))]
    [MemoryPackUnion(54, typeof(CardConfigOptions.MirrorMatch))]
    [MemoryPackUnion(55, typeof(CardConfigOptions.DimensionRift))]
    public partial interface ICardConfig
    {
        CardType Type { get; set; }
        int ManaCost { get; set; }
        CardTarget Target { get; }
    }

    [MemoryPackable]
    [SharedGrainState(Table = "configs", State = "card_config", Key = GrainKeyType.String, Lookup = "CardConfig")]
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

        public Lockdown Lockdown_Normal { get; set; } = new();

        public Sonar Sonar_Normal { get; set; } = new();

        public Purge Purge_Normal { get; set; } = new();

        public Adrenaline Adrenaline_Normal { get; set; } = new();

        public ManaSurge ManaSurge_Normal { get; set; } = new();

        public BloodPact BloodPact_Normal { get; set; } = new();

        public CoinToss CoinToss_Normal { get; set; } = new();

        public ManaFountain ManaFountain_Normal { get; set; } = new();

        public Focus Focus_Normal { get; set; } = new();

        public Shield Shield_Normal { get; set; } = new();

        public PowerSurge PowerSurge_Normal { get; set; } = new();

        public Embargo Embargo_Normal { get; set; } = new();

        public Recycler Recycler_Normal { get; set; } = new();

        public MysticDraw MysticDraw_Normal { get; set; } = new();

        public DoubleOrNothing DoubleOrNothing_Normal { get; set; } = new();

        public GamblersRuin GamblersRuin_Normal { get; set; } = new();

        public Excavator Excavator_Normal { get; set; } = new();
        public Excavator Excavator_Max { get; set; } = new() { Size = 5 };

        public ThermalVision ThermalVision_Normal { get; set; } = new();
        public ThermalVision ThermalVision_Max { get; set; } = new() { Size = 5 };

        public ChaosDiamond ChaosDiamond_Normal { get; set; } = new();

        public ChaosScout ChaosScout_Normal { get; set; } = new();

        public MineCluster MineCluster_Normal { get; set; } = new();
        public MineCluster MineCluster_Max { get; set; } = new() { Size = 3 };

        public CarpetBomb CarpetBomb_Normal { get; set; } = new();
        public CarpetBomb CarpetBomb_Max { get; set; } = new() { Length = 7 };

        public FortuneBlast FortuneBlast_Normal { get; set; } = new();

        public ChaosFog ChaosFog_Normal { get; set; } = new();

        public Frost Frost_Normal { get; set; } = new();
        public Frost Frost_Max { get; set; } = new() { Size = 3 };

        public Blackout Blackout_Normal { get; set; } = new();
        public Blackout Blackout_Max { get; set; } = new() { Size = 3 };

        public FortuneCookie FortuneCookie_Normal { get; set; } = new();

        public Salvage Salvage_Normal { get; set; } = new();

        public CardThief CardThief_Normal { get; set; } = new();

        public SabotageDeck SabotageDeck_Normal { get; set; } = new();

        public Dud Dud_Normal { get; set; } = new();

        public SoulLink SoulLink_Normal { get; set; } = new();

        public MirrorMatch MirrorMatch_Normal { get; set; } = new();

        public DimensionRift DimensionRift_Normal { get; set; } = new();

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
            { CardType.OpponentFlagErase_Max, OpponentFlagErase_Max },

            { CardType.OpponentFlagReshuffle, OpponentFlagReshuffle_Normal },
            { CardType.OpponentFlagReshuffle_Max, OpponentFlagReshuffle_Max },

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

            { CardType.Lockdown, Lockdown_Normal },

            { CardType.Sonar, Sonar_Normal },

            { CardType.Purge, Purge_Normal },

            { CardType.Adrenaline, Adrenaline_Normal },

            { CardType.ManaSurge, ManaSurge_Normal },

            { CardType.BloodPact, BloodPact_Normal },

            { CardType.CoinToss, CoinToss_Normal },

            { CardType.ManaFountain, ManaFountain_Normal },

            { CardType.Focus, Focus_Normal },

            { CardType.Shield, Shield_Normal },

            { CardType.PowerSurge, PowerSurge_Normal },

            { CardType.Embargo, Embargo_Normal },

            { CardType.Recycler, Recycler_Normal },

            { CardType.MysticDraw, MysticDraw_Normal },

            { CardType.DoubleOrNothing, DoubleOrNothing_Normal },

            { CardType.GamblersRuin, GamblersRuin_Normal },

            { CardType.Excavator, Excavator_Normal },
            { CardType.Excavator_Max, Excavator_Max },

            { CardType.ThermalVision, ThermalVision_Normal },
            { CardType.ThermalVision_Max, ThermalVision_Max },

            { CardType.ChaosDiamond, ChaosDiamond_Normal },

            { CardType.ChaosScout, ChaosScout_Normal },

            { CardType.MineCluster, MineCluster_Normal },
            { CardType.MineCluster_Max, MineCluster_Max },

            { CardType.CarpetBomb, CarpetBomb_Normal },
            { CardType.CarpetBomb_Max, CarpetBomb_Max },

            { CardType.FortuneBlast, FortuneBlast_Normal },

            { CardType.ChaosFog, ChaosFog_Normal },

            { CardType.Frost, Frost_Normal },
            { CardType.Frost_Max, Frost_Max },

            { CardType.Blackout, Blackout_Normal },
            { CardType.Blackout_Max, Blackout_Max },

            { CardType.FortuneCookie, FortuneCookie_Normal },

            { CardType.Salvage, Salvage_Normal },

            { CardType.CardThief, CardThief_Normal },

            { CardType.SabotageDeck, SabotageDeck_Normal },

            { CardType.Dud, Dud_Normal },

            { CardType.SoulLink, SoulLink_Normal },

            { CardType.MirrorMatch, MirrorMatch_Normal },

            { CardType.DimensionRift, DimensionRift_Normal },
        };

        [MemoryPackable]
        public partial class Bloodhound : ICardConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Trebuchet : ICardConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 1;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ErosionDozer : ICardConfig
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
        public partial class ZipZap : ICardConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
            public int SearchRadius => 4;
        }

        [MemoryPackable]
        public partial class OpponentFlagErase : ICardConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 3;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class OpponentFlagReshuffle : ICardConfig
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
        public partial class Smoke : ICardConfig
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
        public partial class MinefieldScout : ICardConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 5;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Siphon : ICardConfig
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
            public int MaxChain { get; set; } = 3;
            public int SearchRadius { get; set; } = 4;
            public int SpawnSize { get; set; } = 2;
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
        public partial class FogOfWar : ICardConfig
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

        [MemoryPackable]
        public partial class Lockdown : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
            public int Duration { get; set; } = 2;
            public int MovesReduction { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class Sonar : ICardConfig
        {
            public CardType Type { get; set; }
            public int Size { get; set; } = 4;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class Purge : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class ManaSurge : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int ManaGain { get; set; } = 3;
        }

        [MemoryPackable]
        public partial class Adrenaline : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int ExtraMoves => 1;
        }

        [MemoryPackable]
        public partial class BloodPact : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 0;
            public CardTarget Target => CardTarget.Self;
            public int HpCost => 1;
            public int ManaGain { get; set; } = 3;
            public int ExtraMoves { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class CoinToss : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int WinMoves { get; set; } = 2;
            public int LoseMoves { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class ManaFountain : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int MinMana { get; set; } = 1;
            public int MaxMana { get; set; } = 5;
        }

        [MemoryPackable]
        public partial class Focus : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int Discount => 1;
        }

        [MemoryPackable]
        public partial class Shield : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class PowerSurge : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Self;
            public int Discount { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class Embargo : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
            public int CostIncrease { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class Recycler : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int DrawCount { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class MysticDraw : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int WinDraw { get; set; } = 2;
            public int LoseReturn { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class DoubleOrNothing : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class GamblersRuin : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int WinDraw { get; set; } = 3;
            public int WinMana { get; set; } = 2;
            public int LoseDiscard { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class Excavator : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ThermalVision : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public int Size { get; set; } = 3;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        [MemoryPackable]
        public partial class ChaosDiamond : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
            public int MinSize { get; set; } = 2;
            public int MaxSize { get; set; } = 5;
        }

        [MemoryPackable]
        public partial class ChaosScout : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OwnBoard;
            public int MinLength { get; set; } = 3;
            public int MaxLength { get; set; } = 7;
        }

        [MemoryPackable]
        public partial class MineCluster : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class CarpetBomb : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 5;
            public int Length { get; set; } = 5;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        [MemoryPackable]
        public partial class FortuneBlast : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int MinSize { get; set; } = 1;
            public int MaxSize { get; set; } = 4;
        }

        [MemoryPackable]
        public partial class ChaosFog : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int MinSize { get; set; } = 1;
            public int MaxSize { get; set; } = 4;
            public int Duration => 3;
        }

        [MemoryPackable]
        public partial class Frost : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int Duration { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class Blackout : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int Duration { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class FortuneCookie : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
            public int MinMines { get; set; } = 1;
            public int MaxMines { get; set; } = 3;
        }

        [MemoryPackable]
        public partial class Salvage : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
            public int PeekCount { get; set; } = 3;
        }

        [MemoryPackable]
        public partial class CardThief : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
        }

        [MemoryPackable]
        public partial class SabotageDeck : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Opponent;
        }

        [MemoryPackable]
        public partial class Dud : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 99;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class SoulLink : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
            public int Duration { get; set; } = 2;
        }

        [MemoryPackable]
        public partial class MirrorMatch : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class DimensionRift : ICardConfig
        {
            public CardType Type { get; set; }
            public int ManaCost { get; set; } = 4;
            public int Size { get; set; } = 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }
    }
}