using GamePlay.Loop;
using GamePlay.Services;
using GamePlay.UI;
using Internal;
using Meta;
using UnityEngine;

namespace GamePlay.Players.Buffs
{
    [DisallowMultipleComponent]
    public class PlayerOpponentBuffs : MonoBehaviour, ISceneService, IScopeSetup, IRemotePlayerCreated
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private PlayerBuffView _viewPrefab;
        [SerializeField] private PlayerBuffInfo _infoPrefab;

        private IModifiersRegistry _modifiers;
        private IGameInfoOverlay _infoOverlay;
        private IGameInput _input;
        private IUpdater _updater;
        private PlayerBuffsList _list;

        [Inject]
        internal void Construct(
            IModifiersRegistry modifiers,
            IGameInfoOverlay infoOverlay,
            IGameInput input,
            IUpdater updater)
        {
            _modifiers = modifiers;
            _infoOverlay = infoOverlay;
            _input = input;
            _updater = updater;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IRemotePlayerCreated>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var info = SpawnInfo();
            _list = new PlayerBuffsList(_container, _viewPrefab, info, _modifiers, _input, showOnRight: false);
            _updater.Add(lifetime, _list);
        }

        public void OnRemotePlayer(IReadOnlyLifetime lifetime, IGamePlayer other)
        {
            _list.Bind(lifetime, other.Modifiers);
        }

        private PlayerBuffInfo SpawnInfo()
        {
            var info = _infoOverlay.Spawn(_infoPrefab);
            info.Hide();
            return info;
        }
    }
}
