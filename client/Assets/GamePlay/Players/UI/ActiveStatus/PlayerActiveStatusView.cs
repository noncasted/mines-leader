using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerActiveStatusView : MonoBehaviour, IEntityComponent, IScopeLoaded
    {
        [SerializeField] private SpriteRenderer _frame;
        [SerializeField] private Color _activeColor = Color.white;
        [SerializeField] private Color _inactiveColor = new(0.5f, 0.5f, 0.5f, 1f);

        private IGameRound _round;
        private IGamePlayerInfo _playerInfo;

        [Inject]
        private void Construct(IGameRound round, IGamePlayerInfo playerInfo)
        {
            _round = round;
            _playerInfo = playerInfo;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeLoaded>();
        }

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _round.Player.View(lifetime, player =>
            {
                bool isActive = player != null && player.Info.Id == _playerInfo.Id;
                _frame.color = isActive ? _activeColor : _inactiveColor;
            });
        }
    }
}
