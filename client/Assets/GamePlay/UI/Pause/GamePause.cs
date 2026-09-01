using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.UI;
using Internal;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GamePlay.UI
{
    public interface IGamePause
    {
        void Open();
    }

    [DisallowMultipleComponent]
    public class GamePause : MonoBehaviour, IScopeSetup, ISceneService, IGamePause
    {
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _leaveButton;

        [SerializeField] private GamePauseLeave _gamePauseLeave;

        private IUIStateMachine _stateMachine;
        private IGameState _gameState;
        private IGamePauseSettings _settings;

        [Inject]
        internal void Construct(IUIStateMachine stateMachine, IGameState gameState, IGamePauseSettings settings)
        {
            _gameState = gameState;
            _stateMachine = stateMachine;
            _settings = settings;

            gameObject.SetActive(false);
            _gamePauseLeave.gameObject.SetActive(false);
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
            _settingsButton.ListenClick(lifetime, () => ProcessSettings(lifetime).Forget());
            _leaveButton.ListenClick(lifetime, () => ProcessLeaveMenu(lifetime).Forget());
        }

        private async UniTask ProcessSettings(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(false);
            await _settings.Process(lifetime);
            gameObject.SetActive(true);
        }

        private async UniTask ProcessLeaveMenu(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(false);

            var result = await _gamePauseLeave.Process(lifetime);

            if (result == false)
            {
                gameObject.SetActive(true);
                return;
            }

            _gameState.OnLeave();
        }
    }
}