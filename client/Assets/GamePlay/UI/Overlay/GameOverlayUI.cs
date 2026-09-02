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
        [SerializeField] private Canvas _centerCanvas;

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
            // TEMP DEBUG: chasing the editor/build mismatch of the overlay canvas camera.
            Debug.Log($"[OverlayUI] Create begin {DescribeCanvases()}");

            builder.RegisterComponent(this)
                   .As<IGameOverlayUI>()
                   .As<IScopeSetup>();

            Debug.Log($"[OverlayUI] Create end {DescribeCanvases()}");
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // TEMP DEBUG: chasing the editor/build mismatch of the overlay canvas camera.
            Debug.Log($"[OverlayUI] OnSetup begin {DescribeCanvases()} " +
                      $"gameCamera={(_camera == null ? "null service" : _camera.Camera == null ? "null camera" : _camera.Camera.name)}");

            _pauseButton.ListenClick(lifetime, () => _pause.Open());
            _canvas.worldCamera = _camera.Camera;
            _centerCanvas.worldCamera = _camera.Camera;

            Debug.Log($"[OverlayUI] OnSetup end {DescribeCanvases()}");
        }

        // TEMP DEBUG helper.
        private string DescribeCanvases()
        {
            return $"canvas=[{Describe(_canvas)}] centerCanvas=[{Describe(_centerCanvas)}]";

            static string Describe(Canvas canvas)
            {
                if (canvas == null)
                    return "null";

                var camera = canvas.worldCamera == null ? "null" : canvas.worldCamera.name;
                var rect = (RectTransform)canvas.transform;

                return $"{canvas.name} active={canvas.gameObject.activeInHierarchy} enabled={canvas.enabled} " +
                       $"mode={canvas.renderMode} worldCamera={camera} scale={rect.lossyScale} size={rect.rect.size}";
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _centerCanvas.gameObject.SetActive(true);
        }

        [Button]
        private void OnValidate()
        {
            if (_canvas.worldCamera != null && _centerCanvas.worldCamera != null)
                return;

            var gameCamera = Object.FindAnyObjectByType<GameCamera>();
            
            if (gameCamera == null)
                return;
            
            _canvas.worldCamera = gameCamera.Camera;
            _centerCanvas.worldCamera = gameCamera.Camera;
        }
    }
}