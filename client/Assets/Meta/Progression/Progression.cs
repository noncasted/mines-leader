using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public interface IProgression
    {
        IViewableList<IProgressionMilestone> Milestones { get; }
        IViewableProperty<int> CurrentProgress { get; }

        UniTask<LootBoxOffer> OpenLootBox(IProgressionMilestone milestone);
        UniTask ChooseLootReward(Guid boxId, CardType chosenCard);
    }

    public readonly struct LootBoxOffer
    {
        public Guid BoxId { get; }
        public IReadOnlyList<ICardDefinition> Definitions { get; }
        public IReadOnlyList<CardType> CardTypes { get; }

        public LootBoxOffer(Guid boxId, IReadOnlyList<ICardDefinition> definitions, IReadOnlyList<CardType> cardTypes)
        {
            BoxId = boxId;
            Definitions = definitions;
            CardTypes = cardTypes;
        }
    }

    public class ProgressionService : IProgression, IScopeSetup
    {
        public ProgressionService(
            IBackendProjection<SharedBackendUser.ProgressionProjection> progressionProjection,
            IBackendProjection<SharedBackendUser.LootProjection> lootProjection,
            ILootProgressionConfigs lootConfig,
            IMetaBackend backend,
            ICardsRegistry cardsRegistry)
        {
            _progressionProjection = progressionProjection;
            _lootProjection = lootProjection;
            _lootConfig = lootConfig;
            _backend = backend;
            _cardsRegistry = cardsRegistry;
        }

        private readonly IBackendProjection<SharedBackendUser.ProgressionProjection> _progressionProjection;
        private readonly IBackendProjection<SharedBackendUser.LootProjection> _lootProjection;
        private readonly ILootProgressionConfigs _lootConfig;
        private readonly IMetaBackend _backend;
        private readonly ICardsRegistry _cardsRegistry;

        private readonly ViewableList<ProgressionMilestone, IProgressionMilestone> _milestones = new();
        private readonly List<ProgressionMilestone> _rawMilestones = new();
        private readonly ViewableProperty<int> _currentProgress = new(0);

        private SharedBackendUser.LootProjection _lastLootProjection;

        public IViewableList<IProgressionMilestone> Milestones => _milestones;
        public IViewableProperty<int> CurrentProgress => _currentProgress;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _lootConfig.Listen(lifetime, OnConfigReceived);
            _progressionProjection.Listen(lifetime, OnProgressionUpdated);
            _lootProjection.Listen(lifetime, OnLootUpdated);
        }

        public async UniTask<LootBoxOffer> OpenLootBox(IProgressionMilestone milestone)
        {
            if (milestone.BoxId.Value == Guid.Empty)
                throw new InvalidOperationException("Milestone has no available box.");

            var response = await _backend.OpenLootBox(milestone.BoxId.Value);

            var definitions = response.Choices
                                      .Where(cardType => _cardsRegistry.Entries.ContainsKey(cardType))
                                      .Select(cardType => _cardsRegistry.Entries[cardType])
                                      .ToList();

            return new LootBoxOffer(response.LootBoxId, definitions, response.Choices);
        }

        public UniTask ChooseLootReward(Guid boxId, CardType chosenCard)
        {
            return _backend.ChooseLootReward(boxId, chosenCard);
        }

        private void OnConfigReceived(LootProgressionOptions config)
        {
            _milestones.Clear();
            _rawMilestones.Clear();

            var thresholds = config.Thresholds
                                   .OrderBy(threshold => threshold)
                                   .ToList();

            foreach (var threshold in thresholds)
            {
                var milestone = new ProgressionMilestone(threshold, ProgressionMilestoneStatus.Locked);
                _rawMilestones.Add(milestone);
                _milestones.Add(milestone);
            }

            RefreshState();
        }

        private void OnProgressionUpdated(SharedBackendUser.ProgressionProjection projection)
        {
            _currentProgress.Set(projection.Experience);
            RefreshState();
        }

        private void OnLootUpdated(SharedBackendUser.LootProjection projection)
        {
            _lastLootProjection = projection;
            RefreshState();
        }

        private void RefreshState()
        {
            if (_rawMilestones.Count == 0)
                return;

            var awardedCount = _lastLootProjection?.AwardedCount ?? 0;

            var availableBoxIds = _lastLootProjection?.Boxes
                                                     .Select(entry => entry.Id)
                                                     .ToList() ??
                                  new List<Guid>();
            var claimedCount = awardedCount - availableBoxIds.Count;

            for (var i = 0; i < _rawMilestones.Count; i++)
            {
                var milestone = _rawMilestones[i];

                if (i < claimedCount)
                {
                    milestone.SetStatus(ProgressionMilestoneStatus.Unlocked);
                    milestone.SetBoxId(Guid.Empty);
                }
                else if (i < awardedCount)
                {
                    var boxIndex = i - claimedCount;
                    var boxId = boxIndex < availableBoxIds.Count ? availableBoxIds[boxIndex] : Guid.Empty;
                    milestone.SetStatus(ProgressionMilestoneStatus.Active);
                    milestone.SetBoxId(boxId);
                }
                else
                {
                    var status = _currentProgress.Value >= milestone.Required
                        ? ProgressionMilestoneStatus.Active
                        : ProgressionMilestoneStatus.Locked;

                    milestone.SetStatus(status);
                    milestone.SetBoxId(Guid.Empty);
                }
            }
        }
    }
}