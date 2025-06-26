using Global.Inputs;
using Internal;
using UnityEngine;

namespace Menu.Social
{
    public interface IMenuPlayerInput
    {
        Vector2 MovementDirection { get; }
    }
    
    public class MenuPlayerInput : IMenuPlayerInput, IScopeSetup
    {
        public MenuPlayerInput(IMenuChatUI chatUI)
        {
            _chatUI = chatUI;
            _controls = new Controls();
        }

        private readonly Controls _controls;
        private readonly IMenuChatUI _chatUI;

        private Vector2 _movementDirection;

        public Vector2 MovementDirection => _movementDirection;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _controls.Enable();
            lifetime.Listen(_controls.Disable);

            _controls.Menu.Movement.Listen(lifetime, value =>
            {
                if (_chatUI.IsSelected)
                {
                    _movementDirection = Vector2.zero;
                    return;
                }
                
                _movementDirection = value.ReadValue<Vector2>();
            });
        }
    }
}