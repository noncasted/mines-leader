using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using UnityEngine;
using VContainer;

namespace Menu.Profile
{
    public interface IMenuProfile : IUIState
    {
    }

    [DisallowMultipleComponent]
    public class MenuProfile : MonoBehaviour,
                               IMenuProfile,
                               ISceneService,
                               IScopeSetup,
                               IUIStateAsyncEnterHandler
    {
        [SerializeField] private MenuProfileStats _stats;
        [SerializeField] private MenuProfileHistory _history;
        [SerializeField] private MenuProfileMatch _match;
        [SerializeField] private int _historyCount = Meta.Profile.DefaultHistoryCount;

        private IProfile _profile;
        private IGameModesRegistry _gameModes;
        private ICardsRegistry _cardsRegistry;
        private ICardDescriptionProvider _descriptions;
        private ICardConfigs _configs;

        private ILifetime _requestLifetime;
        private Guid _selectedMatch;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        internal void Construct(
            IProfile profile,
            IGameModesRegistry gameModes,
            ICardsRegistry cardsRegistry,
            ICardDescriptionProvider descriptions,
            ICardConfigs configs)
        {
            _profile = profile;
            _gameModes = gameModes;
            _cardsRegistry = cardsRegistry;
            _descriptions = descriptions;
            _configs = configs;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuProfile>()
                   .As<IScopeSetup>();
        }

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
            handle.AttachGameObject(gameObject);

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
                matches = await _profile.LoadHistory(_historyCount);
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