using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.UI;
using Global.UI.Toolkit;
using Internal;
using Meta;
using Shared;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;
using Position = UnityEngine.UIElements.Position;

namespace Menu.Screens
{
    public interface IMenuProgression : IUIState
    {
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class MenuProgression : MonoBehaviour,
                                   IMenuProgression,
                                   ISceneService,
                                   IScopeSetup,
                                   IUIStateAsyncEnterHandler
    {
        [SerializeField] private VisualTreeAsset _chestTemplate;
        [SerializeField] private VisualTreeAsset _lootOverlayTemplate;
        [SerializeField] private VisualTreeAsset _cardTemplate;

        private IBackendProjection<SharedBackendUser.ProgressionProjection> _progressionProjection;
        private IBackendProjection<SharedBackendUser.LootProjection> _lootProjection;
        private ILootProgressionConfigs _lootConfig;
        private IMetaBackend _backend;
        private ICardsRegistry _cardsRegistry;
        private ICardConfigs _cardConfigs;

        private VisualElement _root;
        private VisualElement _progressionRoot;
        private VisualElement _chestRow;
        private VisualElement _thresholdRow;
        private VisualElement _barFill;
        private VisualElement _xpMarker;
        private Label _xpLabel;
        private Button _exitButton;
        private VisualElement _bottomBar;

        private readonly List<ProgressionMilestone> _milestones = new();
        private readonly List<Label> _thresholdLabels = new();

        private LootProgressionOptions _cachedConfig;
        private SharedBackendUser.LootProjection _lastLootProjection;
        private int _currentXp;
        private int _maxXp;
        private bool _uiReady;
        private bool _isOpeningLootBox;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        private void Construct(
            IBackendProjection<SharedBackendUser.ProgressionProjection> progressionProjection,
            IBackendProjection<SharedBackendUser.LootProjection> lootProjection,
            ILootProgressionConfigs lootConfig,
            IMetaBackend backend,
            ICardsRegistry cardsRegistry,
            ICardConfigs cardConfigs)
        {
            _progressionProjection = progressionProjection;
            _lootProjection = lootProjection;
            _lootConfig = lootConfig;
            _backend = backend;
            _cardsRegistry = cardsRegistry;
            _cardConfigs = cardConfigs;
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

            EnsureUiInitialized();

            _progressionRoot.Show();
            _bottomBar?.Hide();

            handle.InnerLifetime.Listen(() => {
                if (_progressionRoot != null)
                    _progressionRoot.Hide();
                _bottomBar?.Show();
            });

            RebuildMilestones();
            UpdateMilestoneBoxes(_lastLootProjection);
            UpdateBar();
            WireMilestoneClicks(handle.InnerLifetime);

            var completion = new UniTaskCompletionSource();

            _exitButton.ListenClick(handle.InnerLifetime, () => completion.TrySetResult());

            void OnKeyDown(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Escape)
                    completion.TrySetResult();
            }

            _progressionRoot.RegisterCallback<KeyDownEvent>(OnKeyDown);
            handle.InnerLifetime.Listen(
                () => _progressionRoot.UnregisterCallback<KeyDownEvent>(OnKeyDown));

            await completion.Task;
        }

        private void EnsureUiInitialized()
        {
            if (_uiReady)
                return;

            var document = GetComponent<UIDocument>();
            _root = document.rootVisualElement;

            if (_root == null)
                return;

            _progressionRoot = _root.Q<VisualElement>("progression-root");
            _chestRow = _root.Q<VisualElement>("chest-row");
            _thresholdRow = _root.Q<VisualElement>("threshold-row");
            _barFill = _root.Q<VisualElement>("bar-fill");
            _xpMarker = _root.Q<VisualElement>("xp-marker");
            _xpLabel = _root.Q<Label>("xp-label");
            _exitButton = _root.Q<Button>("btn-exit");

            _bottomBar = FindBottomBar();

            _uiReady = true;
        }

        private VisualElement FindBottomBar()
        {
            var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            var self = GetComponent<UIDocument>();

            foreach (var doc in docs)
            {
                if (doc == self)
                    continue;
                var bar = doc.rootVisualElement?.Q<VisualElement>("bottom-bar");

                if (bar != null)
                    return doc.rootVisualElement;
            }

            return null;
        }

        private void OnConfigReceived(LootProgressionOptions config)
        {
            _cachedConfig = config;
            RebuildMilestones();
            UpdateMilestoneBoxes(_lastLootProjection);
            UpdateBar();
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

        private void RebuildMilestones()
        {
            if (!_uiReady || _cachedConfig == null)
                return;

            _chestRow.Clear();
            _thresholdRow.Clear();
            _milestones.Clear();
            _thresholdLabels.Clear();

            var thresholds = _cachedConfig.Thresholds
                                          .OrderBy(t => t)
                                          .ToList();

            _maxXp = thresholds.Count > 0 ? thresholds.Max() : 1;

            foreach (var threshold in thresholds)
            {
                var normalized = _maxXp > 0 ? (float)threshold / _maxXp : 0f;

                var chestContainer = _chestTemplate.CloneTree();
                var chestRoot = chestContainer.Q<VisualElement>("chest");
                chestRoot.RemoveFromHierarchy();

                SetNormalizedLeftCentered(chestRoot, normalized);
                _chestRow.Add(chestRoot);

                var milestone = new ProgressionMilestone(chestRoot);
                milestone.Setup(threshold);
                _milestones.Add(milestone);

                var label = new Label(threshold.ToString());
                label.AddToClassList("threshold-label");
                label.AddToClassList("u-ithaca");
                SetNormalizedLeftCentered(label, normalized);
                _thresholdRow.Add(label);
                _thresholdLabels.Add(label);
            }
        }

        private void WireMilestoneClicks(IReadOnlyLifetime lifetime)
        {
            foreach (var milestone in _milestones)
            {
                var local = milestone;

                local.Button.ListenClick(lifetime, () => {
                    if (!local.HasAvailableBox || _isOpeningLootBox)
                        return;

                    OpenLootBox(local.BoxId, lifetime).Forget();
                });
            }
        }

        private void UpdateBar()
        {
            if (!_uiReady || _milestones.Count == 0)
                return;

            var fillRatio = _maxXp > 0 ? Mathf.Clamp01((float)_currentXp / _maxXp) : 0f;
            _barFill.style.width = Length.Percent(fillRatio * 100f);
            SetNormalizedLeftCentered(_xpMarker, fillRatio);
            _xpLabel.text = $"{_currentXp} XP";

            for (var i = 0; i < _milestones.Count; i++)
            {
                _milestones[i].SetReached(_currentXp >= _milestones[i].RequiredXp);

                if (_currentXp >= _milestones[i].RequiredXp)
                    _thresholdLabels[i].AddToClassList("reached");
                else
                    _thresholdLabels[i].RemoveFromClassList("reached");
            }
        }

        private void UpdateMilestoneBoxes(SharedBackendUser.LootProjection lootProjection)
        {
            if (!_uiReady || lootProjection?.Boxes == null || _milestones.Count == 0)
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
                    milestone.SetReached(true);
                    milestone.SetClaimed(true);
                    milestone.SetAvailableBox(Guid.Empty);
                }
                else if (i < awardedCount && availableBoxes.Count > 0)
                {
                    milestone.SetReached(true);
                    milestone.SetClaimed(false);
                    milestone.SetAvailableBox(availableBoxes.Dequeue());
                }
                else
                {
                    milestone.SetReached(_currentXp >= milestone.RequiredXp);
                    milestone.SetClaimed(false);
                    milestone.SetAvailableBox(Guid.Empty);
                }
            }
        }

        private async UniTask OpenLootBox(Guid boxId, IReadOnlyLifetime lifetime)
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

                var choicePanel = new LootBoxChoicePanel(_lootOverlayTemplate, _cardTemplate, _cardConfigs);
                var chosenCard = await choicePanel.ShowAndAwait(
                    _progressionRoot,
                    lifetime,
                    definitions,
                    response.Choices);

                await _backend.ChooseLootReward(boxId, chosenCard);
            }
            finally
            {
                _isOpeningLootBox = false;
            }
        }

        private static void SetNormalizedLeftCentered(VisualElement element, float normalized)
        {
            element.style.position = Position.Absolute;
            element.style.left = Length.Percent(normalized * 100f);
            element.style.translate = new StyleTranslate(new Translate(Length.Percent(-50f), 0));
        }
    }
}
