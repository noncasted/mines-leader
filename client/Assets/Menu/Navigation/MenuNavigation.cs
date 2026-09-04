using Global.UI;
using Internal;
using Menu.Common;
using Menu.Decks;
using Menu.Play;
using Menu.Profile;
using Menu.Settings;
using Menu.Unlocks;

namespace Menu.Navigation
{
    public interface IMenuNavigation
    {
    }

    public class MenuNavigation : IMenuNavigation, IScopeSetup
    {
        public MenuNavigation(
            IMenuDecks decks,
            IMenuPlay play,
            IMenuUnlocks unlocksScreen,
            IMenuProfile profile,
            IUIStateMachine stateMachine,
            IMenuSettings settings,
            MenuCanvasBindings canvasBindings)
        {
            _profile = profile;
            _settings = settings;
            _canvasBindings = canvasBindings;
            _stateMachine = stateMachine;
            _unlocks = unlocksScreen;
            _decks = decks;
            _play = play;
            
            _unlocksButton = canvasBindings.Navigation.Unlocks.MenuNavigationButton;
            _deckButton = canvasBindings.Navigation.Deck.MenuNavigationButton;
            _playButton = canvasBindings.Navigation.Play.MenuNavigationButton;
            _profileButton = canvasBindings.Navigation.Profile.MenuNavigationButton;
            _settingsButton = canvasBindings.Navigation.Settings.MenuNavigationButton;
        }

        private readonly MenuNavigationButton _unlocksButton;
        private readonly MenuNavigationButton _deckButton;
        private readonly MenuNavigationButton _playButton;
        private readonly MenuNavigationButton _profileButton;
        private readonly MenuNavigationButton _settingsButton;

        private readonly IMenuDecks _decks;
        private readonly IMenuPlay _play;
        private readonly IMenuProfile _profile;
        private readonly IMenuUnlocks _unlocks;
        private readonly IUIStateMachine _stateMachine;
        private readonly IMenuSettings _settings;
        private readonly MenuCanvasBindings _canvasBindings;
        
        private IUIStateHandle _current;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var allButtons = new[]
            {
                _unlocksButton,
                _deckButton,
                _playButton,
                _profileButton,
                _settingsButton
            };

            _unlocksButton.Button.ListenClick(lifetime, () => OnClicked(_unlocksButton, _unlocks));
            _deckButton.Button.ListenClick(lifetime, () => OnClicked(_deckButton, _decks));
            _playButton.Button.ListenClick(lifetime, () => OnClicked(_playButton, _play));
            _profileButton.Button.ListenClick(lifetime, () => OnClicked(_profileButton, _profile));
            _settingsButton.Button.ListenClick(lifetime, () => OnClicked(_settingsButton, _settings));

            _canvasBindings.Navigation.GameObject.SetActive(true);
            OnClicked(_playButton, _play);

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