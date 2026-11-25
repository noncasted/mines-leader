using GamePlay.Services;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardsPointer
    {
        void Add(IReadOnlyLifetime lifetime, ICardPointerTarget target);
    }

    public interface ICardPointerTarget
    {
        bool IsActive { get; }
        int SortingLayer { get; }
        int SortingOrder { get; }

        bool IsInside(Vector2 pointerPosition);
        void SetHovered(bool value);
        void SetPressed(bool value);
    }

    // Card hit test without physics: it used to be done by BoxCollider2D and OnMouse*. Under the pointer
    // the card drawn on top wins. The press, like OnMouseDown/Up, is held by the card
    // the button was pressed on, even if the pointer has left it.
    public class CardsPointer : ICardsPointer, IScopeSetup, IUpdatable
    {
        public CardsPointer(IUpdater updater, IGameInput input)
        {
            _updater = updater;
            _input = input;
        }

        private readonly IUpdater _updater;
        private readonly IGameInput _input;

        private readonly ModifiableList<ICardPointerTarget> _targets = new();

        private ICardPointerTarget _hovered;
        private ICardPointerTarget _pressed;
        private bool _wasDown;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void Add(IReadOnlyLifetime lifetime, ICardPointerTarget target)
        {
            _targets.Add(target);

            lifetime.Listen(() => {
                _targets.Remove(target);

                if (_hovered == target)
                    _hovered = null;

                if (_pressed == target)
                    _pressed = null;
            });
        }

        public void OnUpdate(float delta)
        {
            UpdateHovered(FindTop(_input.World));
            UpdatePressed(_input.Open.Value);
        }

        private ICardPointerTarget FindTop(Vector2 pointerPosition)
        {
            ICardPointerTarget top = null;

            foreach (var target in _targets)
            {
                if (target.IsActive == false || target.IsInside(pointerPosition) == false)
                    continue;

                if (top == null || IsAbove(target, top) == true)
                    top = target;
            }

            return top;
        }

        private void UpdateHovered(ICardPointerTarget target)
        {
            if (target != _hovered)
            {
                _hovered?.SetHovered(false);
                _hovered = target;
            }

            // Like OnMouseOver: confirmed every frame, so a card hovered during
            // a pause is highlighted right after it.
            _hovered?.SetHovered(true);
        }

        private void UpdatePressed(bool isDown)
        {
            // A disabled card releases the press, as OnDisable used to do.
            if (_pressed != null && _pressed.IsActive == false)
                Release();

            if (isDown == true && _wasDown == false)
            {
                _pressed = _hovered;
                _pressed?.SetPressed(true);
            }
            else if (isDown == false && _wasDown == true)
            {
                Release();
            }

            _wasDown = isDown;
        }

        private void Release()
        {
            var pressed = _pressed;
            _pressed = null;
            pressed?.SetPressed(false);
        }

        private static bool IsAbove(ICardPointerTarget target, ICardPointerTarget other)
        {
            if (target.SortingLayer != other.SortingLayer)
                return target.SortingLayer > other.SortingLayer;

            return target.SortingOrder > other.SortingOrder;
        }
    }
}
