using Internal;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamePlay.UI.CardInfo
{
    [DisallowMultipleComponent]
    public class CardInfoDisplayUI : MonoBehaviour, ISceneService
    {
        [SerializeField] private float _fadeInDuration = 0.15f;
        [SerializeField] private float _fadeOutDuration = 0.1f;

        private VisualElement _cardPreview;
        private Label _cardName;
        private Label _cardDescription;

        private float _targetAlpha;
        private float _currentAlpha;
        private float _fadeSpeed;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this);
        }

        private void Awake()
        {
            var document = GetComponent<UIDocument>();
            if (document == null)
            {
                Debug.LogError("[CardInfoDisplayUI] UIDocument not found");
                return;
            }

            var root = document.rootVisualElement;
            _cardPreview = root.Q<VisualElement>("card-preview");
            _cardName = root.Q<Label>("card-name");
            _cardDescription = root.Q<Label>("card-description");

            if (_cardPreview != null)
                _cardPreview.style.opacity = 0f;
        }

        private void Update()
        {
            if (_cardPreview == null)
                return;

            if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
            {
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
                _cardPreview.style.opacity = _currentAlpha;
            }
        }

        public void DisplayCard(string cardName, string description)
        {
            if (_cardName != null)
                _cardName.text = cardName;
            if (_cardDescription != null)
                _cardDescription.text = description;

            _targetAlpha = 1f;
            _fadeSpeed = 1f / _fadeInDuration;
        }

        public void Hide()
        {
            _targetAlpha = 0f;
            _fadeSpeed = 1f / _fadeOutDuration;
        }

        public void HideImmediately()
        {
            _targetAlpha = 0f;
            _currentAlpha = 0f;
            if (_cardPreview != null)
                _cardPreview.style.opacity = 0f;
        }
    }
}