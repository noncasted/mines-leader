using Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Screens.Cards.Preview
{
    [DisallowMultipleComponent]
    public class MenuCardPreviewPopup : MonoBehaviour
    {
        [SerializeField] private RawImage _previewImage;
        [SerializeField] private RectTransform _popupTransform;
        [SerializeField] private CanvasGroup _canvasGroup;

        public void Show(RenderTexture texture)
        {
            if (_previewImage != null)
                _previewImage.texture = texture;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;

            gameObject.SetActive(false);

            if (_previewImage != null)
                _previewImage.texture = null;
        }

        public void SetPosition(Vector2 anchoredPosition)
        {
            if (_popupTransform != null)
                _popupTransform.anchoredPosition = anchoredPosition;
        }
    }
}
