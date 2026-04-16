using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Global.UI.Toolkit;
using Internal;
using Menu.Decks;
using Meta;
using Shared;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Menu.Screens
{
    public interface IMenuHistory : IUIState
    {
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class MenuHistory : MonoBehaviour,
                               IMenuHistory,
                               ISceneService,
                               IScopeSetup,
                               IUIStateAsyncEnterHandler
    {
        [SerializeField] private VisualTreeAsset _cardTemplate;

        private const int HistoryBlockSize = 30;
        private const int CardSlotCount = 5;

        private IMetaBackend _backend;
        private IBackendProjection<SharedBackendUser.RatingProjection> _ratingProjection;
        private IBackendProjection<SharedBackendUser.ProgressionProjection> _progressionProjection;
        private ICardsRegistry _cardsRegistry;
        private ICardConfigs _cardConfigs;

        private readonly List<CardElement> _opponentSlots = new();
        private readonly List<CardElement> _ownSlots = new();
        private readonly List<Button> _historyEntries = new();

        private VisualElement _root;
        private VisualElement _historyRoot;
        private Label _ratingValue;
        private Label _progressionValue;
        private ScrollView _historyList;
        private VisualElement _opponentCardsRow;
        private VisualElement _ownCardsRow;
        private Label _roundTime;
        private Label _ratingChange;
        private Label _progressionChange;
        private VisualElement _loadingOverlay;
        private VisualElement _detailsEmpty;
        private Button _exitButton;
        private VisualElement _bottomBar;

        private Button _selectedEntry;
        private bool _uiReady;
        private bool _isLoadingDetails;
        private int _lastRating;
        private int _lastProgression;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        private void Construct(
            IMetaBackend backend,
            IBackendProjection<SharedBackendUser.RatingProjection> ratingProjection,
            IBackendProjection<SharedBackendUser.ProgressionProjection> progressionProjection,
            ICardsRegistry cardsRegistry,
            ICardConfigs cardConfigs)
        {
            _backend = backend;
            _ratingProjection = ratingProjection;
            _progressionProjection = progressionProjection;
            _cardsRegistry = cardsRegistry;
            _cardConfigs = cardConfigs;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuHistory>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _ratingProjection.Listen(lifetime, OnRatingUpdated);
            _progressionProjection.Listen(lifetime, OnProgressionUpdated);
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);
            EnsureUiInitialized();

            _historyRoot.Show();
            _bottomBar?.Hide();

            handle.InnerLifetime.Listen(() => {
                if (_historyRoot != null)
                    _historyRoot.Hide();
                _bottomBar?.Show();
            });

            UpdateStats();
            ResetDetails();

            await LoadHistory(handle.InnerLifetime);

            var completion = new UniTaskCompletionSource();

            _exitButton.ListenClick(handle.InnerLifetime, () => completion.TrySetResult());

            void OnKeyDown(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Escape)
                    completion.TrySetResult();
            }

            _historyRoot.RegisterCallback<KeyDownEvent>(OnKeyDown);
            handle.InnerLifetime.Listen(
                () => _historyRoot.UnregisterCallback<KeyDownEvent>(OnKeyDown));

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

            _historyRoot = _root.Q<VisualElement>("history-root");
            _ratingValue = _root.Q<Label>("rating-value");
            _progressionValue = _root.Q<Label>("progression-value");
            _historyList = _root.Q<ScrollView>("history-list");
            _opponentCardsRow = _root.Q<VisualElement>("opponent-cards");
            _ownCardsRow = _root.Q<VisualElement>("own-cards");
            _roundTime = _root.Q<Label>("round-time");
            _ratingChange = _root.Q<Label>("rating-change");
            _progressionChange = _root.Q<Label>("progression-change");
            _loadingOverlay = _root.Q<VisualElement>("loading-overlay");
            _detailsEmpty = _root.Q<VisualElement>("details-empty");
            _exitButton = _root.Q<Button>("btn-exit");

            _bottomBar = FindBottomBar();

            BuildCardSlots(_opponentCardsRow, _opponentSlots);
            BuildCardSlots(_ownCardsRow, _ownSlots);

            _uiReady = true;
        }

        private VisualElement FindBottomBar()
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
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

        private void BuildCardSlots(VisualElement row, List<CardElement> slots)
        {
            row.Clear();
            slots.Clear();

            for (var i = 0; i < CardSlotCount; i++)
            {
                var container = _cardTemplate.CloneTree();
                var cardRoot = container.Q<VisualElement>("card-root");
                cardRoot.RemoveFromHierarchy();
                row.Add(cardRoot);

                var element = new CardElement(cardRoot);
                cardRoot.style.display = DisplayStyle.None;
                slots.Add(element);
            }
        }

        private void OnRatingUpdated(SharedBackendUser.RatingProjection projection)
        {
            _lastRating = projection.Rating;
            UpdateStats();
        }

        private void OnProgressionUpdated(SharedBackendUser.ProgressionProjection projection)
        {
            _lastProgression = projection.Experience;
            UpdateStats();
        }

        private void UpdateStats()
        {
            if (!_uiReady)
                return;

            _ratingValue.text = _lastRating.ToString();
            _progressionValue.text = _lastProgression.ToString();
        }

        private async UniTask LoadHistory(IReadOnlyLifetime lifetime)
        {
            _historyList.Clear();
            _historyEntries.Clear();
            _selectedEntry = null;

            SharedBackendUser.Match firstMatch = null;
            Button firstEntry = null;

            try
            {
                var response = await _backend.GetMatchHistory(HistoryBlockSize);

                if (lifetime.IsTerminated || response?.Matches == null)
                    return;

                var ownId = _backend.User.Id;

                foreach (var match in response.Matches)
                {
                    var matchLocal = match;
                    var entry = BuildHistoryEntry(match, ownId);

                    entry.ListenClick(lifetime, () => OnMatchClicked(matchLocal, entry, lifetime));

                    _historyList.Add(entry);
                    _historyEntries.Add(entry);

                    if (firstEntry == null)
                    {
                        firstMatch = match;
                        firstEntry = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MenuHistory] Failed to load match history: {ex.Message}");
            }

            if (firstMatch != null && firstEntry != null)
                OnMatchClicked(firstMatch, firstEntry, lifetime);
        }

        private Button BuildHistoryEntry(SharedBackendUser.Match match, Guid ownId)
        {
            var entry = new Button { text = string.Empty };
            entry.AddToClassList("history-entry");

            var date = new Label(FormatDate(match.Date));
            date.AddToClassList("u-ithaca");
            date.AddToClassList("history-entry-date");
            entry.Add(date);

            var won = match.Winner == ownId;
            var result = new Label(won ? "WIN" : "LOSE");
            result.AddToClassList("u-ithaca");
            result.AddToClassList("history-entry-result");
            result.AddToClassList(won ? "win" : "lose");
            entry.Add(result);

            return entry;
        }

        private void OnMatchClicked(SharedBackendUser.Match match, Button entry, IReadOnlyLifetime lifetime)
        {
            if (_isLoadingDetails)
                return;

            SetSelectedEntry(entry);
            LoadMatchDetails(match.Id, lifetime).Forget();
        }

        private void SetSelectedEntry(Button entry)
        {
            if (_selectedEntry != null)
                _selectedEntry.RemoveFromClassList("selected");

            _selectedEntry = entry;

            if (_selectedEntry != null)
                _selectedEntry.AddToClassList("selected");
        }

        private async UniTask LoadMatchDetails(Guid matchId, IReadOnlyLifetime lifetime)
        {
            if (_isLoadingDetails)
                return;

            _isLoadingDetails = true;
            ShowLoading(true);

            try
            {
                var details = await _backend.GetMatchDetails(matchId);

                if (lifetime.IsTerminated || details == null)
                    return;

                RenderDetails(details);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MenuHistory] Failed to load match details: {ex.Message}");
            }
            finally
            {
                _isLoadingDetails = false;
                ShowLoading(false);
            }
        }

        private void RenderDetails(SharedBackendUser.MatchDetailsResponse details)
        {
            FillCards(_opponentSlots, details.OpponentCards);
            FillCards(_ownSlots, details.OwnCards);

            _roundTime.text = FormatDuration(details.Time);
            _ratingChange.text = FormatSigned(details.RatingChange);
            _progressionChange.text = FormatSigned(details.ProgressionChange);

            ApplySign(_ratingChange, details.RatingChange);
            ApplySign(_progressionChange, details.ProgressionChange);

            _detailsEmpty?.AddToClassList("hidden");
        }

        private void FillCards(List<CardElement> slots, IReadOnlyList<CardType> cards)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (i < cards.Count && _cardsRegistry.Entries.TryGetValue(cards[i], out var definition))
                {
                    var config = _cardConfigs.Value.All[cards[i]];
                    slot.SetCard(definition, config);
                    slot.Root.style.display = DisplayStyle.Flex;
                }
                else
                {
                    slot.Root.style.display = DisplayStyle.None;
                }
            }
        }

        private void ResetDetails()
        {
            foreach (var slot in _opponentSlots)
                slot.Root.style.display = DisplayStyle.None;

            foreach (var slot in _ownSlots)
                slot.Root.style.display = DisplayStyle.None;

            if (_roundTime != null)
                _roundTime.text = "-";

            if (_ratingChange != null)
            {
                _ratingChange.text = "-";
                _ratingChange.RemoveFromClassList("positive");
                _ratingChange.RemoveFromClassList("negative");
            }

            if (_progressionChange != null)
            {
                _progressionChange.text = "-";
                _progressionChange.RemoveFromClassList("positive");
                _progressionChange.RemoveFromClassList("negative");
            }

            _detailsEmpty?.RemoveFromClassList("hidden");
        }

        private void ShowLoading(bool visible)
        {
            if (_loadingOverlay == null)
                return;

            if (visible)
                _loadingOverlay.AddToClassList("visible");
            else
                _loadingOverlay.RemoveFromClassList("visible");
        }

        private static void ApplySign(Label label, int value)
        {
            label.RemoveFromClassList("positive");
            label.RemoveFromClassList("negative");

            if (value > 0)
                label.AddToClassList("positive");
            else if (value < 0)
                label.AddToClassList("negative");
        }

        private static string FormatDate(DateTime date)
        {
            var local = date.ToLocalTime();
            return $"{local:dd.MM HH:mm}";
        }

        private static string FormatDuration(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
        }

        private static string FormatSigned(int value)
        {
            if (value > 0)
                return $"+{value}";
            return value.ToString();
        }
    }
}
