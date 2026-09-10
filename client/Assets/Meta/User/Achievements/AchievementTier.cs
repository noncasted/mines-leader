using Internal;
using Shared;

namespace Meta
{
    public enum AchievementStatus
    {
        /// <summary>Тир недоступен: предыдущие тиры ряда ещё не открыты.</summary>
        Locked,

        /// <summary>Ближайший неоткрытый тир ряда — тот, к которому игрок идёт сейчас.</summary>
        InProgress,

        /// <summary>Условие выполнено, награда ждёт выбора.</summary>
        Available,

        /// <summary>Награда забрана.</summary>
        Taken
    }

    public interface IAchievementTier
    {
        InGameAchievementType Type { get; }
        int Tier { get; }
        string Description { get; }
        IInGameAchievementReward Reward { get; }
        long Target { get; }

        IViewableProperty<long> Progress { get; }
        IViewableProperty<AchievementStatus> Status { get; }
        IViewableProperty<CardType?> RewardCard { get; }
    }

    public class AchievementTier : IAchievementTier
    {
        public AchievementTier(InGameAchievementType type, InGameAchievementTierConfig config)
        {
            Type = type;
            Tier = config.Tier;
            Description = config.Description;
            Reward = config.Reward;
            Condition = config.Condition;
            Target = config.Condition == null ? 0 : config.Condition.GetTarget();
        }

        private readonly ViewableProperty<long> _progress = new(0);
        private readonly ViewableProperty<AchievementStatus> _status = new(AchievementStatus.Locked);
        private readonly ViewableProperty<CardType?> _rewardCard = new(null);

        public InGameAchievementType Type { get; }
        public int Tier { get; }
        public string Description { get; }
        public IInGameAchievementReward Reward { get; }
        public IInGameAchievementCondition Condition { get; }
        public long Target { get; }

        public IViewableProperty<long> Progress => _progress;
        public IViewableProperty<AchievementStatus> Status => _status;
        public IViewableProperty<CardType?> RewardCard => _rewardCard;

        internal void SetProgress(long value)
        {
            _progress.Set(value);
        }

        internal void SetStatus(AchievementStatus value)
        {
            _status.Set(value);
        }

        internal void SetRewardCard(CardType? value)
        {
            _rewardCard.Set(value);
        }
    }

    public interface IAchievementRow
    {
        InGameAchievementType Type { get; }
        string Name { get; }
        System.Collections.Generic.IReadOnlyList<IAchievementTier> Tiers { get; }
    }

    public class AchievementRow : IAchievementRow
    {
        public AchievementRow(
            InGameAchievementType type,
            string name,
            System.Collections.Generic.IReadOnlyList<IAchievementTier> tiers)
        {
            Type = type;
            Name = name;
            Tiers = tiers;
        }

        public InGameAchievementType Type { get; }
        public string Name { get; }
        public System.Collections.Generic.IReadOnlyList<IAchievementTier> Tiers { get; }
    }
}