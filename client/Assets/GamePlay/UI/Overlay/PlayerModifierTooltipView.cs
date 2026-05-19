using TMPro;
using UnityEngine;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class PlayerModifierTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private RectTransform _rectTransform;

        public void Show(string description, Vector2 position)
        {
            _descriptionText.text = description;
            _rectTransform.position = position;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
