using System;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Global.UI;
using Internal;
using Meta;
using TMPro;
using UnityEngine;
using VContainer;

namespace Menu.Main
{
    [DisallowMultipleComponent]
    public class MenuPlay : MonoBehaviour, ISceneService, IMenuPlay
    {
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private DesignButton _button;

        private Matchmaking _matchmaking;
        private bool _isInSearch;
        private IUpdater _updater;
        private ILifetime _searchLifetime;
        private float _time;

        private readonly ViewableDelegate<SessionData> _gameFound = new();

        public IViewableDelegate<SessionData> GameFound => _gameFound;

        [Inject]
        private void Construct(
            Matchmaking matchmaking,
            IUpdater updater)
        {
            _updater = updater;
            _matchmaking = matchmaking;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMenuPlay>();
        }

        private void OnEnable()
        {
            var lifetime = this.GetObjectLifetime();
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
                _buttonText.text = "PLAY";
            }
            else
            {
                Search(lifetime).Forget();
            }
        }

        private async UniTask Search(IReadOnlyLifetime lifetime)
        {
            _isInSearch = true;
            _searchLifetime = lifetime.Child();
            _timer.gameObject.SetActive(true);
            _buttonText.text = "CANCEL";
            _time = 0;

            _updater.RunUpdateAction(_searchLifetime, delta =>
            {
                _time += delta;
                var timeSpan = TimeSpan.FromSeconds(_time);
                _timer.text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }).Forget();

            var sessionData = await _matchmaking.SearchGame(lifetime);
            _gameFound.Invoke(sessionData);
        }
    }
}