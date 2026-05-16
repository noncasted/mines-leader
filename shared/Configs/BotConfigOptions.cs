using System.Collections.Generic;

namespace Shared
{
    [SharedGrainState(Table = "configs",
        State = "bot_config",
        Lookup = "BotConfig",
        Key = GrainKeyType.String)]
    public class BotConfigOptions
    {
        public float MatchmakingApplyThreshold { get; set; } = 20f;
        public BotProfile CurrentProfile { get; set; } = BotProfile.Medium;

        public Dictionary<BotProfile, BotProfileConfig> Profiles { get; set; } = new()
        {
            [BotProfile.Easy] = new()
            {
                FlagsPerRound = 3,
                CellsOpenPerRound = 5,
                CardsUsePerRound = 1,
                ActionDelay = 1.0f,
                MinRoundTime = 10f,
                MaxRoundTime = 15f,
                DeckSize = 6,
                Decks = new List<BotDeck>
                {
                    new()
                    {
                        Name = "Scout Pack",
                        Cards = new List<CardType>
                        {
                            CardType.Bloodhound,
                            CardType.ErosionDozer,
                            CardType.MinefieldScout,
                            CardType.Medic,
                            CardType.ManaSurge,
                            CardType.ZipZap,
                        }
                    },
                },
            },
            [BotProfile.Medium] = new()
            {
                FlagsPerRound = 7,
                CellsOpenPerRound = 4,
                CardsUsePerRound = 2,
                ActionDelay = 0.5f,
                MinRoundTime = 7f,
                MaxRoundTime = 12f,
                DeckSize = 6,
                Decks = new List<BotDeck>
                {
                    new()
                    {
                        Name = "Balanced",
                        Cards = new List<CardType>
                        {
                            CardType.Bloodhound,
                            CardType.ErosionDozer,
                            CardType.ZipZap,
                            CardType.Trebuchet,
                            CardType.Medic,
                            CardType.ManaSurge,
                        }
                    },
                    new()
                    {
                        Name = "Aggro",
                        Cards = new List<CardType>
                        {
                            CardType.OpponentBomb,
                            CardType.OpponentFlagErase,
                            CardType.Trebuchet,
                            CardType.ZipZap,
                            CardType.Smoke,
                            CardType.Adrenaline,
                        }
                    },
                },
            },
            [BotProfile.Hard] = new()
            {
                FlagsPerRound = 10,
                CellsOpenPerRound = 3,
                CardsUsePerRound = 2,
                ActionDelay = 0.3f,
                MinRoundTime = 5f,
                MaxRoundTime = 10f,
                DeckSize = 6,
                Decks = new List<BotDeck>
                {
                    new()
                    {
                        Name = "Pure Scout",
                        Cards = new List<CardType>
                        {
                            CardType.Bloodhound, CardType.Bloodhound_Max,
                            CardType.ErosionDozer, CardType.ErosionDozer_Max,
                            CardType.ZipZap, CardType.ZipZap_Max,
                        }
                    },
                    new()
                    {
                        Name = "Flag Hunter",
                        Cards = new List<CardType>
                        {
                            CardType.OpponentFlagErase, CardType.OpponentFlagErase_Max,
                            CardType.OpponentFlagReshuffle, CardType.OpponentFlagReshuffle_Max,
                            CardType.Trebuchet, CardType.Trebuchet_Max,
                        }
                    },
                    new()
                    {
                        Name = "Aggro Damage",
                        Cards = new List<CardType>
                        {
                            CardType.OpponentBomb,
                            CardType.Trebuchet, CardType.Trebuchet_Max,
                            CardType.ChainReaction,
                            CardType.MineCluster, CardType.MineCluster_Max,
                        }
                    },
                    new()
                    {
                        Name = "Control",
                        Cards = new List<CardType>
                        {
                            CardType.FogOfWar, CardType.FogOfWar_Max,
                            CardType.Smoke, CardType.Smoke_Max,
                            CardType.Lockdown,
                            CardType.Embargo,
                        }
                    },
                    new()
                    {
                        Name = "Survival",
                        Cards = new List<CardType>
                        {
                            CardType.Medic,
                            CardType.Shield,
                            CardType.Adrenaline,
                            CardType.BloodPact,
                            CardType.CoinToss,
                            CardType.PowerSurge,
                        }
                    },
                    new()
                    {
                        Name = "Mana Engine",
                        Cards = new List<CardType>
                        {
                            CardType.ManaSurge,
                            CardType.ManaFountain,
                            CardType.Focus,
                            CardType.Overclock,
                            CardType.Recycler,
                            CardType.Scavenger,
                        }
                    },
                    new()
                    {
                        Name = "Cross-Board Assault",
                        Cards = new List<CardType>
                        {
                            CardType.CarpetBomb, CardType.CarpetBomb_Max,
                            CardType.FortuneBlast,
                            CardType.ChaosFog,
                            CardType.DimensionRift,
                            CardType.Blackout,
                        }
                    },
                    new()
                    {
                        Name = "Mixed Balanced",
                        Cards = new List<CardType>
                        {
                            CardType.Bloodhound,
                            CardType.ErosionDozer,
                            CardType.Trebuchet,
                            CardType.Medic,
                            CardType.ZipZap,
                            CardType.ManaSurge,
                        }
                    },
                    new()
                    {
                        Name = "Late Game",
                        Cards = new List<CardType>
                        {
                            CardType.Siphon,
                            CardType.SoulLink,
                            CardType.MirrorMatch,
                            CardType.GamblersRuin,
                            CardType.Gravedigger,
                            CardType.Salvage,
                        }
                    },
                    new()
                    {
                        Name = "Speed",
                        Cards = new List<CardType>
                        {
                            CardType.Adrenaline,
                            CardType.Overclock,
                            CardType.ZipZap,
                            CardType.Sonar,
                            CardType.ManaSurge,
                            CardType.Bloodhound,
                        }
                    },
                },
            },
        };

        public BotProfileConfig CurrentProfileConfig =>
            Profiles.TryGetValue(CurrentProfile, out var config) ? config : new BotProfileConfig();
    }
}
