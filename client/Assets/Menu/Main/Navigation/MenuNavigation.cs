using Global.Settings;
using Global.UI;
using Internal;
using Menu.Decks;
using Menu.Screens;
using UnityEngine;
using VContainer;

namespace Menu.Main
{
    public interface IMenuNavigation
    {
    }

    [DisallowMultipleComponent]
    public class MenuNavigation : MonoBehaviour, IMenuNavigation, ISceneService, IScopeSetup
    {
        [SerializeField] private DesignButton _settings;
        [SerializeField] private DesignButton _cards;
        [SerializeField] private DesignButton _progression;

        private IMenuDecks _decks;
        private IMenuProgression _progressionScreen;
        private IUIStateMachine _stateMachine;
        private ISettings _settingsService;

        [Inject]
        private void Construct(
            IMenuDecks decks,
            IMenuProgression progressionScreen,
            IUIStateMachine stateMachine,
            ISettings settings)
        {
            _settingsService = settings;
            _stateMachine = stateMachine;
            _progressionScreen = progressionScreen;
            _decks = decks;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuNavigation>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _cards.Clicked.Advise(lifetime, () => _stateMachine.ProcessChild(_stateMachine.Base, _decks));
            _progression.Clicked.Advise(lifetime, () => _stateMachine.ProcessChild(_stateMachine.Base, _progressionScreen));
            _settings.Clicked.Advise(lifetime, () => _settingsService.Open());

            gameObject.SetActive(true);
        }
    }
}