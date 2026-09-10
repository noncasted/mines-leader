using GamePlay.Loop;
using GamePlay.Services;
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
        private IGameInput _input;
        private IUpdater _updater;
        private PlayerBuffsList _list;

        internal void Construct(IModifiersRegistry modifiers, IGameInput input, IUpdater updater)
        {
            _modifiers = modifiers;
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
            var canvas = GetComponentInParent<Canvas>();
            var info = Instantiate(_infoPrefab, canvas.transform);
            info.Hide();
            return info;
        }
    }
}
