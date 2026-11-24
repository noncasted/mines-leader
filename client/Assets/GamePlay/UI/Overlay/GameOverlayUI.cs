using GamePlay.Services;
using Internal;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public interface IGameOverlay
    {
        void Show();
    }

    public class GameOverlay : IScopeSetup, IGameOverlay
    {
        public GameOverlay(
            IGameCamera camera,
            IGamePause pause,
            PlayersOverlayUIBindings playersOverlay,
            RoundOverlayUIBindings roundOverlay)
        {
            _pause = pause;
            _playersOverlay = playersOverlay;
            _roundOverlay = roundOverlay;

            _pauseButton = _roundOverlay.PauseButton.Button;

            _playersOverlay.Canvas.worldCamera = camera.Camera;
            _roundOverlay.Canvas.worldCamera = camera.Camera;
        }

        private readonly IGamePause _pause;
        
        private readonly Button _pauseButton;
        
        private readonly PlayersOverlayUIBindings _playersOverlay;
        private readonly RoundOverlayUIBindings _roundOverlay;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _pauseButton.ListenClick(lifetime, () => _pause.Open());
        }

        public void Show()
        {
            _playersOverlay.GameObject.SetActive(true);
            _roundOverlay.GameObject.SetActive(true);
        }
    }
}