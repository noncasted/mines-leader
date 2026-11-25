using System.Collections.Generic;
using MemoryPack;
#if !UNITY_5_3_OR_NEWER
using Newtonsoft.Json;
#endif

namespace Shared
{
    public partial class CardConfigOptions
    {
        // Словарь строится при первом обращении и дальше переиспользуется: раньше каждое обращение
        // создавало новый (68 записей, ~4.7 КБ), а зовут его на каждую карту клиента и в командах бэкенда.
        // Конфиг, заменённый сеттером после первого обращения, All не увидит.
        // Гонка первого обращения безопасна: оба словаря одинаковые, присваивание ссылки атомарно.
#if !UNITY_5_3_OR_NEWER
        [JsonIgnore]
#endif
        [MemoryPackIgnore]
        public IReadOnlyDictionary<CardType, ICardConfig> All => _all ??= CreateAll();

#if !UNITY_5_3_OR_NEWER
        [JsonIgnore]
#endif
        [MemoryPackIgnore]
        private Dictionary<CardType, ICardConfig> _all;

        private Dictionary<CardType, ICardConfig> CreateAll() => new Dictionary<CardType, ICardConfig>()
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
    }
}
