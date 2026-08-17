using System;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Global.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using VContainer;

namespace Menu.Main
{
    public interface IMenuPlay
    {
        IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound { get; }
    }

    [DisallowMultipleComponent]
    public class MenuPlay : MonoBehaviour, ISceneService, IMenuPlay, IScopeSetup
    {
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private DesignButton _button;

        [SerializeField] private GameObject _modeSelection;
        [SerializeField] private DesignButton _timeLimited;
        [SerializeField] private DesignButton _lastManStanding;

        private IMatchmaking _matchmaking;
        private bool _isInSearch;
        private IUpdater _updater;
        private ILifetime _searchLifetime;
        private ILifetime _selectionLifetime;
        private float _time;

        private readonly ViewableDelegate<SharedMatchmaking.MatchResult> _gameFound = new();

        public IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound => _gameFound;

        [Inject]
        internal void Construct(
            IMatchmaking matchmaking,
            IUpdater updater)
        {
            _updater = updater;
            _matchmaking = matchmaking;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuPlay>()
                   .As<IScopeSetup>();

            gameObject.SetActive(false);
            _timer.gameObject.SetActive(false);
        }
        
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _button.ListenClick(lifetime, OnClicked);
        }

        private void OnClicked()
        {
            var lifetime = this.GetObjectLifetime();

            if (_isInSearch == true)
            {
                _isInSearch = false;
                _searchLifetime?.Terminate();
                _timer.gameObject.SetActive(false);
                _matchmaking.CancelSearch(lifetime);
                _buttonText.text = "play";
            }
            else
            {
                ProcessModeSelection().Forget();
            }
        }

        private async UniTask ProcessModeSelection()
        {
            _selectionLifetime?.Terminate();
            _selectionLifetime = this.GetObjectLifetime().Child();
            gameObject.SetActive(true);

            var completion = new UniTaskCompletionSource<(bool, GameMatchType)>();

            _button.ListenClick(_selectionLifetime, () => completion.TrySetResult((false, GameMatchType.Single)));

            _timeLimited.ListenClick(_selectionLifetime,
                () => completion.TrySetResult((true, GameMatchType.TimeLimited)));

            _lastManStanding.ListenClick(_selectionLifetime,
                () => completion.TrySetResult((true, GameMatchType.LastManStanding)));

            _modeSelection.SetActive(true);

            _selectionLifetime.Listen(() => {
                _modeSelection.SetActive(false);
                completion.TrySetCanceled();
            });

            var (confirmed, type) = await completion.Task;

            if (confirmed == true)
            {
                _modeSelection.SetActive(false);
                Search(type).Forget();
            }
            else
            {
                _modeSelection.SetActive(false);
            }

            _selectionLifetime.Terminate();
        }

        private async UniTask Search(GameMatchType type)
        {
            _isInSearch = true;
            _searchLifetime = this.GetObjectLifetime().Child();
            _timer.gameObject.SetActive(true);
            _buttonText.text = "cancel";
            _time = 0;

            _updater.RunUpdateAction(_searchLifetime, delta => {
                                _time += delta;
                                var timeSpan = TimeSpan.FromSeconds(_time);
                                _timer.text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                            }
                        )
                    .Forget();

            var sessionData = await _matchmaking.SearchGame(_searchLifetime, type);
            _gameFound.Invoke(sessionData);
        }
    }
}