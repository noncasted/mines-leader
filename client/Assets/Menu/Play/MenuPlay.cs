using System;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using VContainer;

namespace Menu.Play
{
    public interface IMenuPlay : IUIState
    {
        IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound { get; }
    }

    [DisallowMultipleComponent]
    public class MenuPlay : MonoBehaviour, IMenuPlay, ISceneService, IUIStateAsyncEnterHandler
    {
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private DesignButton _timeLimited;
        [SerializeField] private DesignButton _lastManStanding;

        private readonly ViewableDelegate<SharedMatchmaking.MatchResult> _gameFound = new();

        private IMatchmaking _matchmaking;
        private IUpdater _updater;
        private ILifetime _searchLifetime;
        private bool _isInSearch;
        private GameMatchType _searchType;
        private float _time;

        public IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound => _gameFound;
        public IUIConstraints Constraints { get; } = UIConstraints.Game;

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
            gameObject.SetActive(false);
            _timer.gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuPlay>();
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);

            var lifetime = handle.InnerLifetime;

            _timeLimited.ListenClick(lifetime, () => OnModeClicked(lifetime, GameMatchType.TimeLimited));
            _lastManStanding.ListenClick(lifetime, () => OnModeClicked(lifetime, GameMatchType.LastManStanding));

            lifetime.Listen(StopSearch);
        }

        private void OnModeClicked(IReadOnlyLifetime lifetime, GameMatchType type)
        {
            if (_isInSearch == true)
            {
                var current = _searchType;
                StopSearch();

                if (current == type)
                    return;
            }

            Search(lifetime, type).Forget();
        }

        private void StopSearch()
        {
            if (_isInSearch == false)
                return;

            _isInSearch = false;
            _searchLifetime?.Terminate();
            _timer.gameObject.SetActive(false);
            _matchmaking.CancelSearch(this.GetObjectLifetime());
        }

        private async UniTask Search(IReadOnlyLifetime lifetime, GameMatchType type)
        {
            _isInSearch = true;
            _searchType = type;
            _searchLifetime = lifetime.Child();
            _timer.gameObject.SetActive(true);
            _time = 0;

            _updater.RunUpdateAction(_searchLifetime, delta => {
                            _time += delta;
                            var timeSpan = TimeSpan.FromSeconds(_time);
                            _timer.text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                        })
                    .Forget();

            var sessionData = await _matchmaking.SearchGame(_searchLifetime, type);
            _isInSearch = false;
            _gameFound.Invoke(sessionData);
        }
    }
}
