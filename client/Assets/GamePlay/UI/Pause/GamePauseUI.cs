using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Settings;
using Global.UI;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.UI
{
    public interface IGamePause
    {
        void Open();
    }

    [DisallowMultipleComponent]
    public class GamePauseUI : MonoBehaviour, IScopeSetup, ISceneService, IGamePause
    {
        [SerializeField] private DesignButton _continueButton;
        [SerializeField] private DesignButton _settingsButton;
        [SerializeField] private DesignButton _leaveButton;

        [SerializeField] private PauseLeaveMenu _pauseLeaveMenu;

        private ISettingsView _settings;
        private IUIStateMachine _stateMachine;
        private IGameState _gameState;

        [Inject]
        internal void Construct(ISettingsView settings, IUIStateMachine stateMachine, IGameState gameState)
        {
            _gameState = gameState;
            _stateMachine = stateMachine;
            _settings = settings;

            gameObject.SetActive(false);
            _pauseLeaveMenu.gameObject.SetActive(false);
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGamePause>()
                   .As<IScopeSetup>();
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _continueButton.ListenClick(lifetime, () => gameObject.SetActive(false));
            _settingsButton.ListenClick(lifetime, () => _stateMachine.ProcessChild(_stateMachine.Base, _settings).Forget());

            _leaveButton.ListenClick(lifetime, () => ProcessLeaveMenu(lifetime).Forget());
        }

        private async UniTask ProcessLeaveMenu(IReadOnlyLifetime lifetime)
        {
            var result = await _pauseLeaveMenu.Process(lifetime);

            if (result == false)
                return;

            _gameState.OnLeave();
        }
    }
}