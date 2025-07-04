using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardPointerHandler
    {
        IViewableProperty<bool> IsHovered { get; }
        IViewableProperty<bool> IsPressed { get; }
    }

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

    public static class CardPointerHandlerExtensions
    {
        public static IReadOnlyLifetime GetUpAwaiterLifetime(
            this ICardPointerHandler pointerHandler,
            IReadOnlyLifetime lifetime)
        {
            var childLifetime = lifetime.Child();
            
            pointerHandler.IsPressed.Advise(childLifetime, value =>
            {
                if (value == true)
                    return;

                childLifetime.Terminate();
            });
            
            return childLifetime;
        }
    }
}