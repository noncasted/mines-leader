using GamePlay.Services;
using Internal;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public interface IGameOverlay
    {
        void Show();
    }

    public class GameOverlay : IScopeBaseSetup, IGameOverlay
    {
        public GameOverlay(
            IGameCamera camera,
            IGamePause pause,
            PlayersOverlayUIBindings playersOverlay,
            RoundOverlayUIBindings roundOverlay)
        {
            _camera = camera;
            _pause = pause;
            _playersOverlay = playersOverlay;
            _roundOverlay = roundOverlay;

            _pauseButton = _roundOverlay.PauseButton.Button;
        }

        private readonly IGameCamera _camera;
        private readonly IGamePause _pause;
        
        private readonly Button _pauseButton;
        
        private readonly PlayersOverlayUIBindings _playersOverlay;
        private readonly RoundOverlayUIBindings _roundOverlay;

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            _playersOverlay.Canvas.worldCamera = _camera.Camera;
            _roundOverlay.Canvas.worldCamera = _camera.Camera;
            _pauseButton.ListenClick(lifetime, () => _pause.Open());
        }

        public void Show()
        {
            _playersOverlay.GameObject.SetActive(true);
            _roundOverlay.GameObject.SetActive(true);
        }
    }
}