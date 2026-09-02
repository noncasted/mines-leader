using TMPro;
using UnityEngine;

namespace GamePlay.Services
{
    [DisallowMultipleComponent]
    public class GameFloatingTextView : MonoBehaviour
    {
        [SerializeField] private Transform _root;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private SpriteRenderer _icon;

        private Color _textColor;
        private Color _iconColor;
        private Vector3 _origin;
        private float _scale;

        public void Setup(GameFloatingTextRequest request)
        {
            _origin = request.Position;
            _scale = request.Scale;

            transform.position = _origin;
            _root.localScale = Vector3.one * _scale;

            _text.text = request.Text ?? string.Empty;
            _text.gameObject.SetActive(string.IsNullOrEmpty(request.Text) == false);

            if (request.TextColor.HasValue == true)
                _text.color = request.TextColor.Value;

            _textColor = _text.color;

            if (request.Icon != null)
            {
                _icon.gameObject.SetActive(true);
                _icon.sprite = request.Icon;

                if (request.IconColor.HasValue == true)
                    _icon.color = request.IconColor.Value;
            }
            else
            {
                _icon.gameObject.SetActive(false);
            }

            _iconColor = _icon.color;
        }

        public void SetOffset(Vector3 offset)
        {
            transform.position = _origin + offset;
        }

        public void SetAlpha(float alpha)
        {
            _text.color = new Color(_textColor.r, _textColor.g, _textColor.b, _textColor.a * alpha);
            _icon.color = new Color(_iconColor.r, _iconColor.g, _iconColor.b, _iconColor.a * alpha);
        }

        public void SetScale(float factor)
        {
            _root.localScale = Vector3.one * (_scale * factor);
        }
    }
}
