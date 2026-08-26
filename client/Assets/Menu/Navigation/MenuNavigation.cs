using Global.Settings;
using Global.UI;
using Internal;
using Menu.Decks;
using Menu.Play;
using Menu.Progression;
using UnityEngine;
using VContainer;

namespace Menu.Navigation
{
    public interface IMenuNavigation
    {
    }

    [DisallowMultipleComponent]
    public class MenuNavigation : MonoBehaviour, IMenuNavigation, ISceneService, IScopeSetup
    {
        [SerializeField] private MenuNavigationButton _progressionButton;
        [SerializeField] private MenuNavigationButton _deckButton;
        [SerializeField] private MenuNavigationButton _playButton;
        [SerializeField] private MenuNavigationButton _statsButton;
        [SerializeField] private MenuNavigationButton _settingsButton;

        private IMenuDecks _decks;
        private IMenuPlay _play;
        private IMenuProgression _progression;
        private IUIStateMachine _stateMachine;
        private ISettingsView _settings;
        private IUIStateHandle _current;

        [Inject]
        internal void Construct(
            IMenuDecks decks,
            IMenuPlay play,
            IMenuProgression progressionScreen,
            IUIStateMachine stateMachine,
            ISettingsView settings)
        {
            _settings = settings;
            _stateMachine = stateMachine;
            _progression = progressionScreen;
            _decks = decks;
            _play = play;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuNavigation>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var allButtons = new[]
            {
                _progressionButton,
                _deckButton,
                _playButton,
                _statsButton,
                _settingsButton
            };

            _progressionButton.Button.ListenClick(lifetime, () => OnClicked(_progressionButton, _progression));
            _deckButton.Button.ListenClick(lifetime, () => OnClicked(_deckButton, _decks));
            _playButton.Button.ListenClick(lifetime, () => OnClicked(_playButton, _play));
            _statsButton.Button.ListenClick(lifetime, () => OnClicked(_statsButton, _progression));
            _settingsButton.Button.ListenClick(lifetime, () => OnClicked(_settingsButton, _settings));
            gameObject.SetActive(true);

            return;

            void OnClicked(MenuNavigationButton button, IUIState child)
            {
                foreach (var check in allButtons)
                {
                    if (check == button)
                        check.Active();
                    else
                        check.Inactive();
                }

                if (_current?.State == child)
                    return;

                _current?.Exit();
                _current = _stateMachine.EnterChild(_stateMachine.Base, child);
            }
        }
    }
}
