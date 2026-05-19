using Internal;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class UIElementPointerHandler :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IEndDragHandler,
        IPointerUpHandler,
        IPointerDownHandler,
        IDragHandler
    {
        private const float _doubleClickThreshold = 0.3f;

        private RectTransform _transform;
        private float _lastClickTime;

        private readonly ViewableProperty<bool> _isHovered = new();
        private readonly ViewableProperty<bool> _isDragging = new();
        private readonly ViewableProperty<bool> _isPressed = new();

        private readonly ViewableDelegate _clicked = new();
        private readonly ViewableDelegate _doubleClicked = new();

        public IViewableProperty<bool> IsHovered => _isHovered;
        public IViewableProperty<bool> IsDragging => _isDragging;
        public IViewableProperty<bool> IsPressed => _isPressed;

        public IViewableDelegate Clicked => _clicked;
        public IViewableDelegate DoubleClicked => _doubleClicked;

        public RectTransform Transform => _transform ??= GetComponent<RectTransform>();

        private void OnEnable()
        {
            _isHovered.Set(false);
            _isDragging.Set(false);
            _isPressed.Set(false);
            _lastClickTime = 0f;
        }

        private void OnDisable()
        {
            _isHovered.Set(false);
            _isDragging.Set(false);
            _isPressed.Set(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered.Set(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered.Set(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed.Set(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed.Set(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var timeSinceLastClick = Time.time - _lastClickTime;

            if (timeSinceLastClick < _doubleClickThreshold)
                _doubleClicked.Invoke();
            else
                _clicked.Invoke();

            _lastClickTime = Time.time;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            _isDragging.Set(true);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            _isDragging.Set(false);
        }

        public void OnDrag(PointerEventData eventData) {}
    }
}