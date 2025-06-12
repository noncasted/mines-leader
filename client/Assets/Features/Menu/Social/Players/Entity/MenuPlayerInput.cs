using Global.Inputs;
using Internal;
using UnityEngine;

namespace Menu
{
    public class MenuPlayerInput : IMenuPlayerInput
    {
        public MenuPlayerInput()
        {
            _controls = new Controls();
        }

        private readonly Controls _controls;

        private Vector2 _movementDirection;

        public Vector2 MovementDirection => _movementDirection;

        public void Setup(IReadOnlyLifetime lifetime)
        {
            _controls.Enable();
            lifetime.Listen(_controls.Disable);

            _controls.Menu.Movement.Listen(lifetime, value => _movementDirection = value.ReadValue<Vector2>());
        }
    }
}