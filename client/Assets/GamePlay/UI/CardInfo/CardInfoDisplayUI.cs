using Global.UI;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class CardInfoDisplayUI : MonoBehaviour, ISceneService
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _cardName;
        [SerializeField] private TMP_Text _cardDescription;
        [SerializeField] private float _fadeInDuration = 0.15f;
        [SerializeField] private float _fadeOutDuration = 0.1f;

        private Coroutine _fadeCoroutine;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this);
        }

        public void DisplayCard(string cardName, string description)
        {
            _cardName.text = cardName;
            _cardDescription.text = description;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        private System.Collections.IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator FadeOut()
        {
            float elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / _fadeOutDuration));
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }
    }
}
