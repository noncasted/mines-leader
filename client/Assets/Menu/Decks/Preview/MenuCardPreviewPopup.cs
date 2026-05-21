using UnityEngine;
using UnityEngine.UI;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuCardPreviewPopup : MonoBehaviour
    {
        [SerializeField] private RawImage _previewImage;
        [SerializeField] private RectTransform _popupTransform;
        [SerializeField] private CanvasGroup _canvasGroup;

        public void Show(RenderTexture texture)
        {
            _previewImage.texture = texture;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            _previewImage.texture = null;
        }
    }
}