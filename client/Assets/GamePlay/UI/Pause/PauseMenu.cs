using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Settings;
using Global.UI;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class PauseMenu : MonoBehaviour, IScopeSetup, ISceneService
    {
        [SerializeField] private DesignButton _enterButton;

        [SerializeField] private DesignButton _continueButton;
        [SerializeField] private DesignButton _settingsButton;
        [SerializeField] private DesignButton _leaveButton;

        [SerializeField] private GameObject _object;

        [SerializeField] private PauseLeaveMenu _pauseLeaveMenu;

        private ISettings _settings;
        private IGameFlow _gameFlow;

        [Inject]
        private void Construct(ISettings settings, IGameFlow gameFlow)
        {
            _gameFlow = gameFlow;
            _settings = settings;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _enterButton.ListenClick(lifetime, () => _object.SetActive(true));
            _continueButton.ListenClick(lifetime, () => _object.SetActive(false));
            _settingsButton.ListenClick(lifetime, () => _settings.Open());

            _leaveButton.ListenClick(lifetime, () => ProcessLeaveMenu(lifetime).Forget());
        }

        private async UniTask ProcessLeaveMenu(IReadOnlyLifetime lifetime)
        {
            var result = await _pauseLeaveMenu.Process(lifetime);
            
            if (result == false)
                return;

            _gameFlow.OnLeave();
        }
    }
}