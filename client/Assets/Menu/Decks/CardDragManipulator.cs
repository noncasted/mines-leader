using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Menu.Decks
{
    public class CardDragManipulator : PointerManipulator
    {
        private readonly VisualElement _dragLayer;
        private readonly Func<VisualElement> _createGhost;
        private readonly Action<VisualElement> _onDropped;
        private readonly Func<bool> _canDrag;

        private VisualElement _ghost;
        private bool _isDragging;
        private int _pointerId;
        private float _cardWidth;
        private float _cardHeight;

        public CardDragManipulator(
            VisualElement dragLayer,
            Func<VisualElement> createGhost,
            Action<VisualElement> onDropped,
            Func<bool> canDrag = null)
        {
            _dragLayer = dragLayer;
            _createGhost = createGhost;
            _onDropped = onDropped;
            _canDrag = canDrag;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_isDragging || evt.button != 0)
                return;

            if (_canDrag != null && !_canDrag())
                return;

            _pointerId = evt.pointerId;
            _isDragging = true;
            _cardWidth = target.resolvedStyle.width;
            _cardHeight = target.resolvedStyle.height;

            _ghost = _createGhost();
            _ghost.style.position = Position.Absolute;
            _ghost.style.width = _cardWidth;
            _ghost.style.height = _cardHeight;
            _ghost.style.opacity = 0.85f;

            UpdateGhostPosition(evt.position);
            _dragLayer.Add(_ghost);

            // Dim the original card
            target.style.unityBackgroundImageTintColor = new Color(0.1f, 0.1f, 0.15f, 1f);

            target.Query<VisualElement>().ForEach(e => {
                e.style.unityBackgroundImageTintColor = new Color(0.1f, 0.1f, 0.15f, 1f);
            });
            target.Query<Label>().ForEach(l => l.style.color = new Color(0.1f, 0.1f, 0.15f, 1f));

            target.CapturePointer(_pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || _ghost == null)
                return;

            UpdateGhostPosition(evt.position);

            var picked = Pick(evt.position);
            HighlightSlot(picked);

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging)
                return;

            var picked = Pick(evt.position);
            var dropTarget = FindDeckSlot(picked);

            CleanupDrag();

            if (dropTarget != null)
                _onDropped?.Invoke(dropTarget);

            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            CleanupDrag();
        }

        private void UpdateGhostPosition(Vector3 pointerPos)
        {
            var local = _dragLayer.WorldToLocal(new Vector2(pointerPos.x, pointerPos.y));
            _ghost.style.left = local.x - _cardWidth / 2;
            _ghost.style.top = local.y - _cardHeight / 2;
        }

        private void CleanupDrag()
        {
            // Restore original card colors
            target.style.unityBackgroundImageTintColor = StyleKeyword.Null;

            target.Query<VisualElement>().ForEach(e => {
                e.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            });
            target.Query<Label>().ForEach(l => l.style.color = StyleKeyword.Null);

            if (_ghost != null)
            {
                _ghost.RemoveFromHierarchy();
                _ghost = null;
            }

            ClearHighlights();
            _isDragging = false;

            if (target.HasPointerCapture(_pointerId))
                target.ReleasePointer(_pointerId);
        }

        private VisualElement Pick(Vector3 position)
        {
            if (_ghost != null)
                _ghost.style.display = DisplayStyle.None;

            var picked = _dragLayer.panel.Pick(new Vector2(position.x, position.y));

            if (_ghost != null)
                _ghost.style.display = DisplayStyle.Flex;

            return picked;
        }

        private VisualElement FindDeckSlot(VisualElement element)
        {
            while (element != null)
            {
                if (element.ClassListContains("deck-slot"))
                    return element;
                element = element.parent;
            }

            return null;
        }

        private VisualElement _currentHighlight;

        private void HighlightSlot(VisualElement picked)
        {
            var slot = FindDeckSlot(picked);

            if (slot == _currentHighlight)
                return;

            ClearHighlights();
            _currentHighlight = slot;
            _currentHighlight?.AddToClassList("drag-hover");
        }

        private void ClearHighlights()
        {
            _currentHighlight?.RemoveFromClassList("drag-hover");
            _currentHighlight = null;
        }
    }
}