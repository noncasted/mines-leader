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

        private Color _defaultTextColor;
        private Color _defaultIconColor;
        private Color _textColor;
        private Color _iconColor;
        private Vector3 _origin;
        private float _scale;

        private void Awake()
        {
            // A pooled view is shown many times: the colours of the prefab are the base of every show.
            _defaultTextColor = _text.color;
            _defaultIconColor = _icon.color;
        }

        public void Setup(GameFloatingTextRequest request)
        {
            _origin = request.Position;
            _scale = request.Scale;

            transform.position = _origin;
            _root.localScale = Vector3.one * _scale;

            _text.text = request.Text ?? string.Empty;
            _text.gameObject.SetActive(string.IsNullOrEmpty(request.Text) == false);

            _textColor = request.TextColor ?? _defaultTextColor;
            _text.color = _textColor;

            _icon.gameObject.SetActive(request.Icon != null);
            _icon.sprite = request.Icon;

            _iconColor = request.IconColor ?? _defaultIconColor;
            _icon.color = _iconColor;
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
