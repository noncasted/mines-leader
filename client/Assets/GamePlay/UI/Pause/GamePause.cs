using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.UI;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public interface IGamePause
    {
        void Open();
    }

    public class GamePause : IScopeSetup, IGamePause
    {
        public GamePause(
            IUIStateMachine stateMachine,
            IGameState gameState,
            IGamePauseSettings settings,
            IGamePauseLeave leave,
            IGameContext gameContext,
            GamePauseMenuBindings bindings)
        {
            _gameState = gameState;
            _stateMachine = stateMachine;
            _settings = settings;
            _leave = leave;
            _gameContext = gameContext;

            var buttons = bindings.Menu.Plate.Buttons;
            _continueButton = buttons.GamePauseContinue.Button;
            _settingsButton = buttons.GamePauseSettings.Button;
            _leaveButton = buttons.GamePauseExit.Button;
            _gameObject = bindings.GameObject;

            _gameObject.SetActive(false);
        }

        private readonly Button _continueButton;
        private readonly Button _settingsButton;
        private readonly Button _leaveButton;
        private readonly GameObject _gameObject;

        private readonly IUIStateMachine _stateMachine;
        private readonly IGameState _gameState;
        private readonly IGamePauseSettings _settings;
        private readonly IGamePauseLeave _leave;
        private readonly IGameContext _gameContext;

        public void Open()
        {
            _gameContext.SetPaused(true);
            _gameObject.SetActive(true);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _continueButton.ListenClick(lifetime, Close);
            _settingsButton.ListenClick(lifetime, () => ProcessSettings(lifetime).Forget());
            _leaveButton.ListenClick(lifetime, () => ProcessLeaveMenu(lifetime).Forget());
        }

        private async UniTask ProcessSettings(IReadOnlyLifetime lifetime)
        {
            _gameObject.SetActive(false);
            await _settings.Process(lifetime);
            _gameObject.SetActive(true);
        }

        private async UniTask ProcessLeaveMenu(IReadOnlyLifetime lifetime)
        {
            _gameObject.SetActive(false);

            var result = await _leave.Process(lifetime);

            if (result == false)
            {
                _gameObject.SetActive(true);
                return;
            }

            _gameContext.SetPaused(false);
            _gameState.OnLeave();
        }

        private void Close()
        {
            _gameObject.SetActive(false);
            _gameContext.SetPaused(false);
        }
    }
}
