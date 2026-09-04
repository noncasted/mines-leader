using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Menu.Play
{
    public interface IMenuPlay : IUIState
    {
        IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound { get; }
    }

    public class MenuPlay : IMenuPlay, IScopeSetup, IUIStateAsyncEnterHandler
    {
        public MenuPlay(
            IMatchmaking matchmaking,
            IMatchMakingConfigs matchMakingConfigs,
            IGameModesRegistry gameModesRegistry,
            IUpdater updater,
            MenuPlayBindings bindings)
        {
            _updater = updater;
            _matchmaking = matchmaking;
            _matchMakingConfigs = matchMakingConfigs;
            _gameModesRegistry = gameModesRegistry;

            _modesRoot = bindings.Modes.RectTransform;
            _searchView = bindings.SearchView.MenuPlaySearchView;
            _gameObject = bindings.GameObject;

            _gameObject.SetActive(false);
            _searchView.ShowIdle();
        }

        private readonly RectTransform _modesRoot;
        private readonly MenuPlaySearchView _searchView;
        private readonly GameObject _gameObject;

        private readonly ViewableDelegate<SharedMatchmaking.MatchResult> _gameFound = new();
        private readonly List<MenuPlayGameMode> _modes = new();

        private readonly IMatchmaking _matchmaking;
        private readonly IMatchMakingConfigs _matchMakingConfigs;
        private readonly IGameModesRegistry _gameModesRegistry;
        private readonly IUpdater _updater;

        private IReadOnlyLifetime _lifetime;
        private ILifetime _searchLifetime;
        private bool _isInSearch;
        private GameMatchType _selectedType;
        private float _time;

        public IViewableDelegate<SharedMatchmaking.MatchResult> MatchFound => _gameFound;
        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime;

            _searchView.SearchButton.ListenClick(lifetime, () => OnSearchClicked(lifetime));
            _searchView.CancelButton.ListenClick(lifetime, StopSearch);
            _matchMakingConfigs.View(lifetime, options => BuildModes(lifetime, options));
        }

        public UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(_gameObject);
            handle.InnerLifetime.Listen(StopSearch);
            return UniTask.CompletedTask;
        }

        private void BuildModes(IReadOnlyLifetime lifetime, MatchMakingOptions options)
        {
            while (_modesRoot.childCount > 0)
                Object.DestroyImmediate(_modesRoot.GetChild(0).gameObject);

            _modes.Clear();

            foreach (var type in options.Available)
            {
                if (_gameModesRegistry.Entries.TryGetValue(type, out var definition) == false)
                    continue;

                var view = Object.Instantiate(MenuPrefabs.GameModeEntry, _modesRoot);
                view.Setup(definition);
                view.ListenClick(lifetime, () => Select(type));
                _modes.Add(view);
            }

            LayoutModes();

            if (_modes.Count > 0)
                Select(_modes[0].Type);
        }

        private void LayoutModes()
        {
            const float width = 277.5f;
            const float spacing = 40f;
            var count = _modes.Count;
            var total = count * width + Mathf.Max(count - 1, 0) * spacing;
            var startX = -total * 0.5f + width * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var rect = (RectTransform)_modes[i].transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width, 457.5f);
                rect.anchoredPosition = new Vector2(startX + i * (width + spacing), 40f);
            }
        }

        private void Select(GameMatchType type)
        {
            _selectedType = type;

            foreach (var mode in _modes)
                mode.SetSelected(mode.Type == type);
        }

        private void OnSearchClicked(IReadOnlyLifetime lifetime)
        {
            if (_isInSearch == true)
                return;

            if (_modes.Count == 0)
                return;

            Search(lifetime, _selectedType).Forget();
        }

        private void StopSearch()
        {
            if (_isInSearch == false)
                return;

            _isInSearch = false;
            _searchLifetime?.Terminate();
            _searchView.ShowIdle();
            _matchmaking.CancelSearch(_lifetime);
        }

        private async UniTask Search(IReadOnlyLifetime lifetime, GameMatchType type)
        {
            _isInSearch = true;
            _searchLifetime = lifetime.Child();
            _searchView.ShowSearching();
            _time = 0;
            _searchView.SetTimer("00:00");

            _updater.RunUpdateAction(_searchLifetime, delta => {
                            _time += delta;
                            var timeSpan = TimeSpan.FromSeconds(_time);
                            _searchView.SetTimer($"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}");
                        })
                    .Forget();

            try
            {
                var sessionData = await _matchmaking.SearchGame(_searchLifetime, type);
                _isInSearch = false;
                _searchView.ShowIdle();
                _gameFound.Invoke(sessionData);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
