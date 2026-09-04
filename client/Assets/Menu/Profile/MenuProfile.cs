using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace Menu.Profile
{
    public interface IMenuProfile : IUIState
    {
    }

    public class MenuProfile : IMenuProfile, IScopeSetup, IUIStateAsyncEnterHandler
    {
        public MenuProfile(
            IProfile profile,
            IGameModesRegistry gameModes,
            ICardsRegistry cardsRegistry,
            ICardDescriptionProvider descriptions,
            ICardConfigs configs,
            MenuProfileBindings bindings)
        {
            _profile = profile;
            _gameModes = gameModes;
            _cardsRegistry = cardsRegistry;
            _descriptions = descriptions;
            _configs = configs;

            _stats = bindings.Top.MenuProfileStats;
            _history = bindings.Matches.MenuProfileHistory;
            _match = bindings.Matches.View.MenuProfileMatch;
            _gameObject = bindings.GameObject;

            _gameObject.SetActive(false);
        }

        // Глубина истории раньше правилась в инспекторе, но так её никто и не трогал.
        private const int HistoryCount = Meta.Profile.DefaultHistoryCount;

        private readonly MenuProfileStats _stats;
        private readonly MenuProfileHistory _history;
        private readonly MenuProfileMatch _match;
        private readonly GameObject _gameObject;

        private readonly IProfile _profile;
        private readonly IGameModesRegistry _gameModes;
        private readonly ICardsRegistry _cardsRegistry;
        private readonly ICardDescriptionProvider _descriptions;
        private readonly ICardConfigs _configs;

        private ILifetime _requestLifetime;
        private Guid _selectedMatch;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _match.Construct(_cardsRegistry, _descriptions, _configs);
            _match.Hide();

            _stats.Bind(lifetime, _profile);

            var avatar = _profile.Character switch
            {
                CharacterType.BIBA or CharacterType.BOBA => Sprites.Portraits.DefaultOwn,
                _ => null
            };

            _stats.SetAvatar(avatar);
        }

        /// <summary>
        /// История приезжает запросом, поэтому перезапрашивается на каждом входе:
        /// между открытиями экрана игрок успевает сыграть.
        /// </summary>
        public UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(_gameObject);

            _requestLifetime?.Terminate();
            _requestLifetime = handle.InnerLifetime.Child();

            var lifetime = _requestLifetime;

            _selectedMatch = Guid.Empty;
            _match.Hide();
            _history.Clear();

            LoadHistory(lifetime).Forget();

            return UniTask.CompletedTask;
        }

        private async UniTaskVoid LoadHistory(IReadOnlyLifetime lifetime)
        {
            IReadOnlyList<ProfileMatchEntry> matches;

            try
            {
                matches = await _profile.LoadHistory(HistoryCount);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (lifetime.IsTerminated == true)
                return;

            _history.Build(lifetime, matches, ModeName, OnMatchSelected);

            if (matches.Count > 0)
                OnMatchSelected(matches[0]);
        }

        private void OnMatchSelected(ProfileMatchEntry entry)
        {
            if (_selectedMatch == entry.Id)
                return;

            _selectedMatch = entry.Id;
            _history.SetSelected(entry.Id);

            LoadDetails(_requestLifetime, entry).Forget();
        }

        private async UniTaskVoid LoadDetails(IReadOnlyLifetime lifetime, ProfileMatchEntry entry)
        {
            ProfileMatchDetails details;

            try
            {
                details = await _profile.LoadDetails(entry.Id);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (lifetime.IsTerminated == true)
                return;

            // Пока ответ ехал, игрок мог выбрать другой матч.
            if (_selectedMatch != entry.Id)
                return;

            if (details == null)
            {
                _match.Hide();
                return;
            }

            _match.Show(details);
        }

        private string ModeName(ProfileMatchEntry entry)
        {
            return _gameModes.Entries.TryGetValue(entry.Type, out var definition) == true
                ? definition.Name
                : entry.Type.ToString();
        }
    }
}
