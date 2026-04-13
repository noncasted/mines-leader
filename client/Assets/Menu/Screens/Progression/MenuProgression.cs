using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using VContainer;

namespace Menu.Screens
{
    public interface IMenuProgression : IUIState
    {
    }

    [DisallowMultipleComponent]
    public class MenuProgression : MonoBehaviour,
                                   IMenuProgression,
                                   ISceneService,
                                   IScopeSetup,
                                   IUIStateAsyncEnterHandler
    {
        [SerializeField] private DesignButton _backButton;
        [SerializeField] private RectTransform _barFill;
        [SerializeField] private RectTransform _barRoot;
        [SerializeField] private TMP_Text _xpText;
        [SerializeField] private RectTransform _milestonesRoot;
        [SerializeField] private ProgressionMilestone _milestonePrefab;

        private IBackendProjection<SharedBackendUser.ProgressionProjection> _progressionProjection;
        private IBackendProjection<SharedBackendUser.LootProjection> _lootProjection;
        private ILootProgressionConfigs _lootConfig;
        private IMetaBackend _backend;
        private ICardsRegistry _cardsRegistry;

        private readonly List<ProgressionMilestone> _milestones = new();
        private SharedBackendUser.LootProjection _lastLootProjection;
        private int _currentXp;
        private bool _initialized;
        private bool _isOpeningLootBox;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        private void Construct(
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

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuProgression>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _lootConfig.Listen(lifetime, OnConfigReceived);
            _progressionProjection.Listen(lifetime, OnProgressionUpdated);
            _lootProjection.Listen(lifetime, OnLootUpdated);
        }
        
        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);
            UpdateBar();

            foreach (var milestone in _milestones)
            {
                milestone.Button.ListenClick(handle.InnerLifetime, () => {
                    if (milestone.HasAvailableBox == false || _isOpeningLootBox)
                        return;

                    OpenLootBox(milestone.BoxId).Forget();
                });
            }

            await _backButton.WaitClick(handle);
        }

        private void OnConfigReceived(LootProgressionOptions config)
        {
            foreach (var milestone in _milestones)
                Destroy(milestone.gameObject);

            _milestones.Clear();

            var thresholds = config.Thresholds
                                   .OrderBy(t => t)
                                   .ToList();

            var maxXp = thresholds.Count > 0 ? thresholds.Max() : 1;

            foreach (var threshold in thresholds)
            {
                var milestone = Instantiate(_milestonePrefab, _milestonesRoot);
                milestone.Setup(threshold, (float)threshold / maxXp);
                _milestones.Add(milestone);
            }

            _initialized = true;
            UpdateBar();
            UpdateMilestoneBoxes(_lastLootProjection);
        }

        private void OnProgressionUpdated(SharedBackendUser.ProgressionProjection projection)
        {
            _currentXp = projection.Experience;
            UpdateMilestoneBoxes(_lastLootProjection);
            UpdateBar();
        }

        private void OnLootUpdated(SharedBackendUser.LootProjection projection)
        {
            _lastLootProjection = projection;
            UpdateMilestoneBoxes(projection);
            UpdateBar();
        }

        private void UpdateBar()
        {
            if (!_initialized || _milestones.Count == 0)
                return;

            var maxXp = _milestones.Max(m => m.RequiredXp);
            var fillRatio = maxXp > 0 ? Mathf.Clamp01((float)_currentXp / maxXp) : 0f;

            var barWidth = _barRoot.rect.width;
            _barFill.sizeDelta = new Vector2(barWidth * fillRatio, 0);

            _xpText.text = $"{_currentXp} XP";
            var xpRt = _xpText.GetComponent<RectTransform>();
            xpRt.anchoredPosition = new Vector2(barWidth * fillRatio, xpRt.anchoredPosition.y);

            foreach (var milestone in _milestones)
                milestone.SetReached(_currentXp >= milestone.RequiredXp);
        }

        private void UpdateMilestoneBoxes(SharedBackendUser.LootProjection lootProjection)
        {
            if (lootProjection?.Boxes == null || !_initialized || _milestones.Count == 0)
                return;

            var sorted = _milestones.OrderBy(m => m.RequiredXp).ToList();
            var awardedCount = lootProjection.AwardedCount;
            var claimedCount = awardedCount - lootProjection.Boxes.Count;
            var availableBoxes = new Queue<Guid>(lootProjection.Boxes.Select(b => b.Id));

            for (var i = 0; i < sorted.Count; i++)
            {
                var milestone = sorted[i];

                if (i < claimedCount)
                {
                    // Already opened
                    milestone.SetReached(true);
                    milestone.SetClaimed(true);
                    milestone.SetAvailableBox(Guid.Empty);
                }
                else if (i < awardedCount && availableBoxes.Count > 0)
                {
                    // Available to open
                    milestone.SetReached(true);
                    milestone.SetClaimed(false);
                    milestone.SetAvailableBox(availableBoxes.Dequeue());
                }
                else
                {
                    // Not yet awarded or locked
                    milestone.SetReached(_currentXp >= milestone.RequiredXp);
                    milestone.SetClaimed(false);
                    milestone.SetAvailableBox(Guid.Empty);
                }
            }
        }
        
        public async UniTask OpenLootBox(Guid boxId)
        {
            if (_isOpeningLootBox)
                return;

            _isOpeningLootBox = true;

            try
            {
                var response = await _backend.OpenLootBox(boxId);

                if (response.Choices == null || response.Choices.Count == 0)
                    return;

                var definitions = response.Choices
                                          .Where(c => _cardsRegistry.Entries.ContainsKey(c))
                                          .Select(c => _cardsRegistry.Entries[c])
                                          .ToList();

                if (definitions.Count == 0)
                    return;

                var choiceCompletion = new UniTaskCompletionSource<CardType>();

                var choicePanel = new GameObject("LootChoicePanel");
                choicePanel.transform.SetParent(transform, false);

                var choiceUI = choicePanel.AddComponent<LootBoxChoicePanel>();
                choiceUI.Setup(definitions, response.Choices, choiceCompletion);

                var chosenCard = await choiceCompletion.Task;

                Destroy(choicePanel);

                await _backend.ChooseLootReward(boxId, chosenCard);
            }
            finally
            {
                _isOpeningLootBox = false;
            }
        }
    }
}