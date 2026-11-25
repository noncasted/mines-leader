using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GamePlay.Players.Buffs
{
    [DisallowMultipleComponent]
    public class PlayerBuffInfo : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private TMP_Text _description;

        private Canvas _canvas;

        public bool IsShown => gameObject.activeSelf;

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Show(string description)
        {
            EnsureCanvas();
            DisableRaycasts();

            _description.text = description;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        }

        public void Follow(Vector2 screenPosition, bool showOnRight)
        {
            if (IsShown == false)
                return;

            EnsureCanvas();

            var parent = (RectTransform)_rectTransform.parent;

            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                screenPosition,
                camera,
                out var localPoint);

            var pivotX = showOnRight == true ? 0f : 1f;
            var pivotY = screenPosition.y > Screen.height * 0.5f ? 1f : 0f;

            _rectTransform.pivot = new Vector2(pivotX, pivotY);
            _rectTransform.anchoredPosition = localPoint;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null)
                return;

            _canvas = GetComponentInParent<Canvas>();
        }

        private void DisableRaycasts()
        {
            var graphics = GetComponentsInChildren<Graphic>(true);

            for (var i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }
    }
}
