using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

namespace Meta
{
    public interface IAchievements
    {
        IViewableList<IAchievementRow> Rows { get; }

        IViewableProperty<int> UnlockedCount { get; }
    }

    public class AchievementsService : IAchievements, IScopeSetup
    {
        public AchievementsService(
            IInGameAchievementConfigs configs,
            IBackendProjection<SharedBackendUser.UserStatsProjection> statsProjection,
            IBackendProjection<SharedBackendUser.InGameAchievementsProjection> achievementsProjection)
        {
            _configs = configs;
            _statsProjection = statsProjection;
            _achievementsProjection = achievementsProjection;
        }

        private readonly IInGameAchievementConfigs _configs;
        private readonly IBackendProjection<SharedBackendUser.UserStatsProjection> _statsProjection;
        private readonly IBackendProjection<SharedBackendUser.InGameAchievementsProjection> _achievementsProjection;

        private readonly ViewableList<AchievementRow, IAchievementRow> _rows = new();

        private readonly List<List<AchievementTier>> _rowTiers = new();
        private readonly ViewableProperty<int> _unlockedCount = new(0);

        private SharedBackendUser.UserStatsProjection _stats;
        private SharedBackendUser.InGameAchievementsProjection _unlocked;

        public IViewableList<IAchievementRow> Rows => _rows;
        public IViewableProperty<int> UnlockedCount => _unlockedCount;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _configs.View(lifetime, OnConfigReceived);
            _statsProjection.View(lifetime, OnStatsReceived);
            _achievementsProjection.View(lifetime, OnAchievementsReceived);
        }

        private void OnConfigReceived(InGameAchievementOptions options)
        {
            _rows.Clear();
            _rowTiers.Clear();

            foreach (var group in options.Groups)
            {
                var tiers = new List<IAchievementTier>();
                var rowTiers = new List<AchievementTier>();

                foreach (var tierConfig in group.Tiers.OrderBy(tier => tier.Tier))
                {
                    var tier = new AchievementTier(group.Type, tierConfig);
                    rowTiers.Add(tier);
                    tiers.Add(tier);
                }

                _rowTiers.Add(rowTiers);
                _rows.Add(new AchievementRow(group.Type, group.Name, tiers));
            }

            Refresh();
        }

        private void OnStatsReceived(SharedBackendUser.UserStatsProjection projection)
        {
            _stats = projection;
            Refresh();
        }

        private void OnAchievementsReceived(SharedBackendUser.InGameAchievementsProjection projection)
        {
            _unlocked = projection;
            Refresh();
        }

        private void Refresh()
        {
            var unlockedEntries = new Dictionary<(InGameAchievementType, int),
                SharedBackendUser.InGameAchievementsProjection.UnlockedAchievement>();

            if (_unlocked != null)
            {
                foreach (var entry in _unlocked.Unlocked)
                    unlockedEntries[(entry.Type, entry.Tier)] = entry;
            }

            foreach (var rowTiers in _rowTiers)
            {
                // Ряд открывается по порядку, поэтому в работе всегда ровно один тир — первый неоткрытый.
                var hasTierInProgress = false;

                foreach (var tier in rowTiers)
                {
                    var progress = tier.Condition == null ? 0 : tier.Condition.GetProgress(_stats);

                    tier.SetProgress(progress);

                    if (unlockedEntries.TryGetValue((tier.Type, tier.Tier), out var entry) == false)
                    {
                        tier.SetStatus(hasTierInProgress == true
                            ? AchievementStatus.Locked
                            : AchievementStatus.InProgress);

                        tier.SetRewardCard(null);
                        hasTierInProgress = true;
                        continue;
                    }

                    tier.SetStatus(entry.Claimed == true ? AchievementStatus.Taken : AchievementStatus.Available);
                    tier.SetRewardCard(entry.RewardCard);
                }
            }

            _unlockedCount.Set(unlockedEntries.Count);
        }
    }
}