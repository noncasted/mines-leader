using MemoryPack;

namespace Shared
{
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
        public Frost Frost_Max { get; set; } = new() { Size = 3, TurnsDuration = 2 };

        public Blackout Blackout_Normal { get; set; } = new();
        public Blackout Blackout_Max { get; set; } = new() { Size = 3, TurnsDuration = 2 };

        public FortuneCookie FortuneCookie_Normal { get; set; } = new();

        public Salvage Salvage_Normal { get; set; } = new();

        public CardThief CardThief_Normal { get; set; } = new();

        public SabotageDeck SabotageDeck_Normal { get; set; } = new();

        public Dud Dud_Normal { get; set; } = new();

        public SoulLink SoulLink_Normal { get; set; } = new();

        public MirrorMatch MirrorMatch_Normal { get; set; } = new();

        public DimensionRift DimensionRift_Normal { get; set; } = new();
    }

    [MemoryPackUnion(1040, typeof(CardConfigOptions))]
    public partial class NetworkContextUnion
    {
    }
}
