using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    /// <summary>
    /// Условие ачивки. Считается по статам игрока, поэтому одинаково работает
    /// и на сервере (состояние грейна), и на клиенте (проекция статов).
    /// </summary>
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(StatThresholdCondition))]
    [MemoryPackUnion(1, typeof(CardGroupPlayedCondition))]
    public partial interface IInGameAchievementCondition
    {
        bool IsConditionMet(IUserStatsState stats);

        long GetProgress(IUserStatsState stats);

        long GetTarget();
    }

    [MemoryPackable]
    public partial class StatThresholdCondition : IInGameAchievementCondition
    {
        public UserStatType Stat { get; set; }
        public long Required { get; set; }

        public bool IsConditionMet(IUserStatsState stats) => GetProgress(stats) >= Required;

        public long GetProgress(IUserStatsState stats) => stats == null ? 0 : stats.Get(Stat);

        public long GetTarget() => Required;
    }

    [MemoryPackable]
    public partial class CardGroupPlayedCondition : IInGameAchievementCondition
    {
        public CardGroup Group { get; set; }
        public long Required { get; set; }

        public bool IsConditionMet(IUserStatsState stats) => GetProgress(stats) >= Required;

        public long GetProgress(IUserStatsState stats) => stats == null ? 0 : stats.GetCardsPlayed(Group);

        public long GetTarget() => Required;
    }

    public enum InGameAchievementRewardType
    {
        None = 0,
        CardUnlock = 100,
    }

    /// <summary>
    /// Награда за ачивку. Новые типы наград добавляются новой реализацией и union-тегом.
    /// </summary>
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardUnlockReward))]
    public partial interface IInGameAchievementReward
    {
        InGameAchievementRewardType RewardType { get; }

        string GetDescription();
    }

    /// <summary>
    /// Открывает игроку новую карту. Если <see cref="PossibleGroups"/> не пуст,
    /// карта выдаётся только из перечисленных групп.
    /// </summary>
    [MemoryPackable]
    public partial class CardUnlockReward : IInGameAchievementReward
    {
        public List<CardGroup> PossibleGroups { get; set; } = new List<CardGroup>();

        public InGameAchievementRewardType RewardType => InGameAchievementRewardType.CardUnlock;

        public string GetDescription()
        {
            if (PossibleGroups == null || PossibleGroups.Count == 0)
                return "New card";

            return "New " + string.Join("/", PossibleGroups) + " card";
        }
    }

    [MemoryPackable]
    public partial class InGameAchievementTierConfig
    {
        public int Tier { get; set; }
        public string Description { get; set; } = string.Empty;
        public IInGameAchievementCondition Condition { get; set; }
        public IInGameAchievementReward Reward { get; set; }
    }

    [MemoryPackable]
    public partial class InGameAchievementGroupConfig
    {
        public InGameAchievementType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<InGameAchievementTierConfig> Tiers { get; set; } = new List<InGameAchievementTierConfig>();
    }

    [MemoryPackable]
    [SharedGrainState(Table = "configs", State = "in_game_achievement_config", Key = GrainKeyType.String,
        Lookup = "InGameAchievementConfig")]
    public partial class InGameAchievementOptions : INetworkContext
    {
        public List<InGameAchievementGroupConfig> Groups { get; set; } = new List<InGameAchievementGroupConfig>();

        public InGameAchievementGroupConfig GetGroup(InGameAchievementType type)
        {
            foreach (var group in Groups)
            {
                if (group.Type == type)
                    return group;
            }

            return null;
        }

        public static InGameAchievementOptions CreateDefault()
        {
            return new InGameAchievementOptions
            {
                Groups = new List<InGameAchievementGroupConfig>
                {
                    StatGroup(InGameAchievementType.FlagsSet, "Flag Bearer", UserStatType.FlagsSet,
                        "Set {0} flags", new long[] { 10, 100, 1000, 10000, 100000 }),

                    StatGroup(InGameAchievementType.CellsOpened, "Excavator", UserStatType.CellsOpened,
                        "Open {0} cells", new long[] { 50, 500, 5000, 50000, 500000 }),

                    StatGroup(InGameAchievementType.MinesDetonated, "Bomb Squad", UserStatType.MinesDetonated,
                        "Detonate {0} mines", new long[] { 5, 50, 500, 5000, 50000 }),

                    StatGroup(InGameAchievementType.CardsPlayed, "Card Slinger", UserStatType.CardsPlayed,
                        "Play {0} cards", new long[] { 25, 250, 2500, 25000, 250000 }),

                    StatGroup(InGameAchievementType.BuffsReceived, "Empowered", UserStatType.BuffsReceived,
                        "Receive {0} buffs", new long[] { 10, 100, 1000, 10000, 100000 }),

                    StatGroup(InGameAchievementType.EnemyCellsPlanted, "Saboteur", UserStatType.EnemyCellsPlanted,
                        "Plant {0} cells on enemy boards", new long[] { 10, 100, 1000, 10000, 100000 }),

                    StatGroup(InGameAchievementType.ManaSpent, "Mana Burner", UserStatType.ManaSpent,
                        "Spend {0} mana", new long[] { 100, 1000, 10000, 100000, 1000000 }),

                    StatGroup(InGameAchievementType.MatchesPlayed, "Veteran", UserStatType.MatchesPlayed,
                        "Play {0} matches", new long[] { 1, 10, 100, 1000, 10000 }),

                    StatGroup(InGameAchievementType.MatchesWon, "Champion", UserStatType.MatchesWon,
                        "Win {0} matches", new long[] { 1, 10, 100, 1000, 10000 }),

                    GroupCardsGroup(InGameAchievementType.ScoutCardsPlayed, "Pathfinder", CardGroup.Scout),
                    GroupCardsGroup(InGameAchievementType.DefenseCardsPlayed, "Bulwark", CardGroup.Defense),
                    GroupCardsGroup(InGameAchievementType.AttackCardsPlayed, "Warmonger", CardGroup.Attack),
                    GroupCardsGroup(InGameAchievementType.BuffCardsPlayed, "Enchanter", CardGroup.Buff),
                    GroupCardsGroup(InGameAchievementType.DebuffCardsPlayed, "Hexweaver", CardGroup.Debuff),
                }
            };
        }

        private static InGameAchievementGroupConfig StatGroup(
            InGameAchievementType type,
            string name,
            UserStatType stat,
            string descriptionFormat,
            long[] thresholds)
        {
            var tiers = new List<InGameAchievementTierConfig>();

            for (var i = 0; i < thresholds.Length; i++)
            {
                tiers.Add(new InGameAchievementTierConfig
                {
                    Tier = i + 1,
                    Description = string.Format(descriptionFormat, thresholds[i]),
                    Condition = new StatThresholdCondition
                    {
                        Stat = stat,
                        Required = thresholds[i]
                    },
                    Reward = new CardUnlockReward()
                });
            }

            return new InGameAchievementGroupConfig
            {
                Type = type,
                Name = name,
                Tiers = tiers
            };
        }

        private static InGameAchievementGroupConfig GroupCardsGroup(
            InGameAchievementType type,
            string name,
            CardGroup group)
        {
            var thresholds = new long[] { 5, 50, 500, 5000, 50000 };
            var tiers = new List<InGameAchievementTierConfig>();

            for (var i = 0; i < thresholds.Length; i++)
            {
                tiers.Add(new InGameAchievementTierConfig
                {
                    Tier = i + 1,
                    Description = "Play " + thresholds[i] + " " + group + " cards",
                    Condition = new CardGroupPlayedCondition
                    {
                        Group = group,
                        Required = thresholds[i]
                    },
                    Reward = new CardUnlockReward
                    {
                        PossibleGroups = new List<CardGroup> { group }
                    }
                });
            }

            return new InGameAchievementGroupConfig
            {
                Type = type,
                Name = name,
                Tiers = tiers
            };
        }
    }
}
