using System;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Global.UI.Toolkit;
using Internal;
using Meta;
using Shared;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Menu.Main
{
    public interface IMenuPlay
    {
        IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound { get; }
    }

    [DisallowMultipleComponent]
    public class MenuPlay : MonoBehaviour, ISceneService, IScopeSetup, IMenuPlay
    {
        private IMenuNavigation _navigation;
        private IMatchmaking _matchmaking;
        private IUpdater _updater;

        private bool _isInSearch;
        private ILifetime _searchLifetime;
        private ILifetime _selectionLifetime;
        private float _time;

        private Button _button;
        private Label _timer;
        private VisualElement _modeSelection;
        private Button _timeLimited;
        private Button _lastManStanding;

        private readonly ViewableDelegate<SharedMatchmaking.MatchResult> _gameFound = new();

        public IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound => _gameFound;

        [Inject]
        private void Construct(
            IMenuNavigation navigation,
            IMatchmaking matchmaking,
            IUpdater updater) {
            _navigation = navigation;
            _updater = updater;
            _matchmaking = matchmaking;
        }

        public void Create(IScopeBuilder builder) {
            builder.RegisterComponent(this)
                   .As<IMenuPlay>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime) {
            var root = _navigation.Root;

            _button = root.Q<Button>("btn-play");
            _timer = root.Q<Label>("timer");
            _modeSelection = root.Q<VisualElement>("mode-selection");
            _timeLimited = root.Q<Button>("btn-time-limited");
            _lastManStanding = root.Q<Button>("btn-last-man");

            _timer.Hide();
            _modeSelection.Hide();

            _button.ListenClick(lifetime, OnClicked);
        }

        private void OnClicked() {
            var lifetime = this.GetObjectLifetime();

            if (_isInSearch) {
                _isInSearch = false;
                _searchLifetime?.Terminate();
                _timer.Hide();
                _matchmaking.CancelSearch(lifetime);
                _button.text ="play";
            }
            else {
                ProcessModeSelection().Forget();
            }
        }

        private async UniTask ProcessModeSelection() {
            _selectionLifetime?.Terminate();
            _selectionLifetime = this.GetObjectLifetime().Child();

            var completion = new UniTaskCompletionSource<(bool, GameMatchType)>();

            _button.ListenClick(_selectionLifetime, () => completion.TrySetResult((false, GameMatchType.Single)));
            _timeLimited.ListenClick(_selectionLifetime, () => completion.TrySetResult((true, GameMatchType.TimeLimited)));
            _lastManStanding.ListenClick(_selectionLifetime, () => completion.TrySetResult((true, GameMatchType.LastManStanding)));

            _modeSelection.Show();

            _selectionLifetime.Listen(() => {
                _modeSelection.Hide();
                completion.TrySetCanceled();
            });

            var (confirmed, type) = await completion.Task;

            _modeSelection.Hide();

            if (confirmed)
                Search(type).Forget();

            _selectionLifetime.Terminate();
        }

        private async UniTask Search(GameMatchType type) {
            _isInSearch = true;
            _searchLifetime = this.GetObjectLifetime().Child();
            _timer.Show();
            _button.text ="cancel";
            _time = 0;

            _updater.RunUpdateAction(_searchLifetime, delta => {
                _time += delta;
                var timeSpan = TimeSpan.FromSeconds(_time);
                _timer.text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }).Forget();

            var sessionData = await _matchmaking.SearchGame(_searchLifetime, type);
            _gameFound.Invoke(sessionData);
        }
    }
}
