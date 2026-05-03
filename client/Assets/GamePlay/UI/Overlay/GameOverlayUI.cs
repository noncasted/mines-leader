using Internal;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace GamePlay.UI
{
    public interface IGameOverlayUI
    {
        void Show();
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class GameOverlayUI : MonoBehaviour, ISceneService, IScopeSetup, IGameOverlayUI
    {
        private IGamePause _pause;

        [Inject]
        private void Construct(IGamePause pause)
        {
            _pause = pause;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGameOverlayUI>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var document = GetComponent<UnityEngine.UIElements.UIDocument>();
            if (document == null)
            {
                Debug.LogError("[GameOverlayUI] UIDocument not found");
                return;
            }

            var root = document.rootVisualElement;
            var pauseButton = root.Q<UnityEngine.UIElements.VisualElement>("pause-button");
            if (pauseButton == null)
            {
                Debug.LogError("[GameOverlayUI] pause-button not found in UXML");
                return;
            }

            pauseButton.RegisterCallback<UnityEngine.UIElements.PointerDownEvent>(_ => _pause.Open());
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}