using Global.UI;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.UI
{
    public interface IGameOverlayUI
    {
        void Show();
    }

    [DisallowMultipleComponent]
    public class GameOverlayUI : MonoBehaviour, ISceneService, IScopeSetup, IGameOverlayUI
    {
        [SerializeField] private DesignButton _pauseButton;

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
            _pauseButton.ListenClick(lifetime, () => _pause.Open());
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}