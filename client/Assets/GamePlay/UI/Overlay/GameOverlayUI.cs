using GamePlay.Services;
using Internal;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace GamePlay.UI
{
    public interface IGameOverlayUI
    {
        void Show();
    }

    [DisallowMultipleComponent]
    public class GameOverlayUI : MonoBehaviour, ISceneService, IScopeSetup, IGameOverlayUI
    {
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Canvas _canvas;

        private IGamePause _pause;
        private IGameCamera _camera;

        [Inject]
        internal void Construct(IGamePause pause, IGameCamera camera)
        {
            _camera = camera;
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
            _pauseButton.ListenClick(lifetime, () => _pause.Open());
            _canvas.worldCamera = _camera.Camera;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        [Button]
        private void OnValidate()
        {
            if (_canvas.worldCamera != null)
                return;

            var gameCamera = Object.FindAnyObjectByType<GameCamera>();
            
            if (gameCamera == null)
                return;
            
            _canvas.worldCamera = gameCamera.Camera;
        }
    }
}