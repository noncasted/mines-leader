using GamePlay.Loop;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Players.ActiveStatus
{
    [DisallowMultipleComponent]
    public class PlayerActiveStatusView : MonoBehaviour, IEntityComponent, IScopeLoaded
    {
        [SerializeField] private SpriteRenderer _frame;

        private IGameRound _round;
        private IGamePlayerInfo _playerInfo;

        [Inject]
        internal void Construct(IGameRound round, IGamePlayerInfo playerInfo)
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
            _round.Player.View(lifetime, player => {
                var isActive = player != null && player.Info.Id == _playerInfo.Id;
                _frame.sprite = isActive == true ? Sprites.GameField.Active : Sprites.GameField.Invactive;
            });
        }
    }
}