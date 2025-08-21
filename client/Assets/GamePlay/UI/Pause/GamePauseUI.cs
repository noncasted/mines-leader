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

        private ISettings _settings;
        private IGameState _gameState;

        [Inject]
        private void Construct(ISettings settings, IGameState gameState)
        {
            _gameState = gameState;
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
            _settingsButton.ListenClick(lifetime, () => _settings.Open());

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