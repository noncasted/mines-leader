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
                CardPlayDelay = 4f,
                EndTurnDelay = 4f,
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
                CardPlayDelay = 4f,
                EndTurnDelay = 4f,
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
                FlagsPerRound = 20,
                CellsOpenPerRound = 8,
                CardsUsePerRound = 6,
                CardPlayDelay = 4f,
                EndTurnDelay = 4f,
                ActionDelay = 0.3f,
                MinRoundTime = 5f,
                MaxRoundTime = 10f,
                DeckSize = 6,
                Decks = new List<BotDeck>
                {
                    // Куратор: разведка кормит солвер, давление держит противника.
                    // Случайный выбор из десятка колод раньше решал за нас, будет ли бот вообще опасен.
                    new()
                    {
                        Name = "Scout Pressure",
                        Cards = new List<CardType>
                        {
                            CardType.Bloodhound,
                            CardType.ErosionDozer,
                            CardType.ZipZap,
                            CardType.Sonar,
                            CardType.Trebuchet,
                            CardType.OpponentFlagErase,
                        }
                    },
                    new()
                    {
                        Name = "Scout Tempo",
                        Cards = new List<CardType>
                        {
                            CardType.Bloodhound,
                            CardType.ErosionDozer,
                            CardType.MinefieldScout,
                            CardType.Adrenaline,
                            CardType.ManaSurge,
                            CardType.Trebuchet,
                        }
                    },
                },
            },
        };

        public BotProfileConfig CurrentProfileConfig =>
            Profiles.TryGetValue(CurrentProfile, out var config) ? config : new BotProfileConfig();
    }
}
