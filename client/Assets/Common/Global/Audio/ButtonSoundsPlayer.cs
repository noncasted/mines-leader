using Internal;

namespace Global.Audio
{
    // Gives the hooks of ButtonSounds (every button of the menu and the game) their sounds and takes them back with
    // the global scope.
    public class ButtonSoundsPlayer : IScopeSetup
    {
        public ButtonSoundsPlayer(IAudioPlayer player)
        {
            _player = player;
        }

        private readonly IAudioPlayer _player;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // The shared sounds are downloaded in parallel with the startup: the buttons are silent until they are.
            ButtonSounds.Clicked = () =>
            {
                if (GlobalAudio.IsLoaded == true)
                    _player.PlayRandomFromGroup(GlobalAudio.UIButtonClick);
            };

            ButtonSounds.Hovered = () =>
            {
                if (GlobalAudio.IsLoaded == true)
                    _player.PlayRandomFromGroup(GlobalAudio.UIElementHover);
            };

            lifetime.Listen(() =>
            {
                ButtonSounds.Clicked = null;
                ButtonSounds.Hovered = null;
            });
        }
    }
}
