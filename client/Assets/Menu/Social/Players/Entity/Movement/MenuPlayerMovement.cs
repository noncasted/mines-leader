using Common.Animations;
using Common.Network;
using Global.Systems;
using Internal;
using MemoryPack;
using UnityEngine;
using VContainer;

namespace Menu.Social
{
    public class MenuPlayerMovement : MonoBehaviour, IEntityComponent, IFixedUpdatable, IScopeSetup
    {
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private float _moveSpeed = 1f;
        [SerializeField] private float _lerpSpeed = 1f;
        [SerializeField] private SpriteRenderer _renderer;

        private NetworkProperty<MenuPlayerTransformState> _state;
        private IUpdater _updater;
        private IMenuPlayerInput _input;
        private INetworkEntity _entity;
        private MenuPlayerIdle _idle;
        private MenuPlayerRun _run;
        private IReadOnlyLifetime _lifetime;
        private ForwardSpriteAnimation _currentAnimation;

        [Inject]
        public void Construct(
            IUpdater updater,
            IMenuPlayerInput input,
            INetworkEntity entity,
            MenuPlayerIdle idle,
            MenuPlayerRun run,
            NetworkProperty<MenuPlayerTransformState> state)
        {
            _run = run;
            _idle = idle;
            _entity = entity;
            _state = state;
            _updater = updater;
            _input = input;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime;
            _updater.Add(lifetime, this);
        }

        public void OnFixedUpdate(float delta)
        {
            if (_entity.Owner.IsLocal == true)
                Local();
            else
                Remote();

            var selectedAnimation = SelectAnimation();

            if (selectedAnimation != _currentAnimation)
            {
                _currentAnimation?.ManualStop();
                selectedAnimation.PlayLooped(_lifetime);
                _currentAnimation = selectedAnimation;
            }
            
            void Local()
            {
                _rb.MovePosition(_rb.position + _input.MovementDirection * (_moveSpeed * delta));
              
                if (_input.MovementDirection.x > 0)
                    _renderer.flipX = false;
                else if (_input.MovementDirection.x < 0)
                    _renderer.flipX = true;
                
                _state.Set(new MenuPlayerTransformState()
                {
                    Position = _rb.position,
                    FlipX = _renderer.flipX,
                    IsRunning = _input.MovementDirection != Vector2.zero
                });
            }

            void Remote()
            {
                if (_state.Value == null)
                    return;

                var target = _state.Value.Position;
                var position = Vector2.Lerp(_rb.position, target, _lerpSpeed * delta);
                _renderer.flipX = _state.Value.FlipX;
                _rb.MovePosition(position);
            }

            ForwardSpriteAnimation SelectAnimation()
            {
                if (_state.Value.IsRunning == false)
                    return _idle;

                return _run;
            }
        }
    }

    [MemoryPackable]
    public partial class MenuPlayerTransformState
    {
        public Vector2 Position { get; set; }
        public bool FlipX { get; set; }
        public bool IsRunning { get; set; }
    }
}