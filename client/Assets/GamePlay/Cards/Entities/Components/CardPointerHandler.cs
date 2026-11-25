using GamePlay.Loop;
using Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace GamePlay.Cards
{
    public interface ICardPointerHandler
    {
        IViewableProperty<bool> IsHovered { get; }
        IViewableProperty<bool> IsPressed { get; }
    }

    // Hover and press are determined by the shared ICardsPointer, here the only rule
    // is added: on pause the card is neither highlighted nor pressed.
    public class CardPointerHandler : ICardPointerHandler, ICardPointerTarget, IScopeSetup
    {
        public CardPointerHandler(
            GameCardBindings bindings,
            ICardsPointer pointer,
            IGameContext gameContext,
            ILifetime containerLifetime)
        {
            _containerLifetime = containerLifetime;
            _area = bindings.View.PointerHandler.CardPointerArea;
            _sortingGroup = bindings.SortingGroup;
            _pointer = pointer;
            _gameContext = gameContext;
        }

        private readonly ILifetime _containerLifetime;
        private readonly CardPointerArea _area;
        private readonly SortingGroup _sortingGroup;
        private readonly ICardsPointer _pointer;
        private readonly IGameContext _gameContext;

        private readonly ViewableProperty<bool> _isHovered = new();
        private readonly ViewableProperty<bool> _isPressed = new();

        public IViewableProperty<bool> IsHovered => _isHovered;
        public IViewableProperty<bool> IsPressed => _isPressed;

        public bool IsActive => _area.IsActive;
        public int SortingLayer => UnityEngine.SortingLayer.GetLayerValueFromID(_sortingGroup.sortingLayerID);
        public int SortingOrder => _sortingGroup.sortingOrder;

        // Not the lifetime from OnSetup: the card is destroyed by Destroy() through the container lifetime,
        // and the scope loader lifetime outlives the view.
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _pointer.Add(_containerLifetime, this);
        }

        public bool IsInside(Vector2 pointerPosition)
        {
            return _area.IsInside(pointerPosition);
        }

        public void SetHovered(bool value)
        {
            _isHovered.Set(value && _gameContext.IsPaused == false);
        }

        public void SetPressed(bool value)
        {
            _isPressed.Set(value && _gameContext.IsPaused == false);
        }
    }

    public static class CardPointerHandlerExtensions
    {
        public static IReadOnlyLifetime GetUpAwaiterLifetime(
            this ICardPointerHandler pointerHandler,
            IReadOnlyLifetime lifetime)
        {
            var childLifetime = lifetime.Child();

            pointerHandler.IsPressed.Advise(childLifetime, value => {
                if (value == true)
                    return;

                childLifetime.Terminate();
            });

            return childLifetime;
        }
    }
}
