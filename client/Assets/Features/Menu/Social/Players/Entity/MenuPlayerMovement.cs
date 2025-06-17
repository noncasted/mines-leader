using Common.Network;
using Common.Network.Common;
using Global.Systems;
using Internal;
using MemoryPack;
using UnityEngine;

namespace Menu
{
    public class MenuPlayerMovement : MonoBehaviour, IFixedUpdatable
    {
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private MenuPlayerOptions _options;

        private NetworkProperty<MenuPlayerTransformState> _state;
        private IUpdater _updater;
        private IMenuPlayerInput _input;
        private INetworkEntity _entity;

        public void Construct(
            IUpdater updater,
            IMenuPlayerInput input,
            INetworkEntity entity,
            NetworkProperty<MenuPlayerTransformState> state)
        {
            _entity = entity;
            _state = state;
            _updater = updater;
            _input = input;
        }

        public void Setup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void OnFixedUpdate(float delta)
        {
            if (_entity.Owner.IsLocal == true)
                Local();
            else
                Remote();

            void Local()
            {
                _rb.MovePosition(_rb.position + _input.MovementDirection * (_options.MoveSpeed * delta));

                _state.Set(new MenuPlayerTransformState()
                {
                    Position = _rb.position
                });
            }

            void Remote()
            {
                if (_state.Value == null)
                    return;
                
                var target = _state.Value.Position;
                var position = Vector2.Lerp(_rb.position, target, _options.LerpSpeed * delta);
                
                _rb.MovePosition(position);
            }
        }
    }

    [MemoryPackable]
    public partial class MenuPlayerTransformState
    {
        public Vector2 Position { get; set; }
    }
}