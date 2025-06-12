using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardPointerHandler : MonoBehaviour, ICardPointerHandler, IEntityComponent
    {
        private readonly ViewableProperty<bool> _isHovered = new();
        private readonly ViewableProperty<bool> _isPressed = new();
        
        public IViewableProperty<bool> IsHovered => _isHovered;
        public IViewableProperty<bool> IsPressed => _isPressed;

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<ICardPointerHandler>();
        }
        
        private void OnMouseEnter()
        {
            _isHovered.Set(true);
        }

        private void OnMouseExit()
        {
            _isHovered.Set(false);
        }

        private void OnMouseDown()
        {
            _isPressed.Set(true);
        }

        private void OnMouseUp()
        {
            _isPressed.Set(false);
        }
    }
}