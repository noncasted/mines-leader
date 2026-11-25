using Cysharp.Threading.Tasks;
using Global.Settings;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public interface IGamePauseSettings
    {
        /// <summary>
        /// Opens the settings over the pause and waits until the player presses "back".
        /// </summary>
        UniTask Process(IReadOnlyLifetime lifetime);
    }

    /// <summary>
    /// The settings screen in the pause: there are no apply buttons, every change goes to the save at once.
    /// </summary>
    public class GamePauseSettings : IGamePauseSettings
    {
        public GamePauseSettings(ISettings settings, GamePauseSettingsBindings bindings)
        {
            _settings = settings;

            var content = bindings.Plate.Content;
            _masterVolume = content.Master.Slider.Slider;
            _musicVolume = content.Music.Slider.Slider;
            _soundsVolume = content.Sounds.Slider.Slider;
            _shakeIntensity = content.Shake.Slider.Slider;
            _vSync = content.Vsync.Setting.GamePauseSettingsSwitch;
            _backButton = bindings.Plate.Back.Button;
            _gameObject = bindings.GameObject;

            _gameObject.SetActive(false);
        }

        private readonly Slider _masterVolume;
        private readonly Slider _musicVolume;
        private readonly Slider _soundsVolume;
        private readonly Slider _shakeIntensity;
        private readonly GamePauseSettingsSwitch _vSync;
        private readonly Button _backButton;
        private readonly GameObject _gameObject;

        private readonly ISettings _settings;

        /// <summary>
        /// The save copy lives exactly as long as the screen is open: values are written into it
        /// on every change and immediately go to <see cref="ISettings.Apply"/>.
        /// </summary>
        public async UniTask Process(IReadOnlyLifetime lifetime)
        {
            _gameObject.SetActive(true);

            var screenLifetime = lifetime.Child();
            var completion = new UniTaskCompletionSource();
            var save = _settings.Copy();

            _masterVolume.SetValueWithoutNotify(save.MasterVolume);
            _musicVolume.SetValueWithoutNotify(save.MusicVolume);
            _soundsVolume.SetValueWithoutNotify(save.SoundsVolume);
            _shakeIntensity.SetValueWithoutNotify(save.ShakeIntensity);

            _masterVolume.onValueChanged.Listen(screenLifetime, value => {
                save.SetMasterVolume(value);
                Apply(save);
            });

            _musicVolume.onValueChanged.Listen(screenLifetime, value => {
                save.SetMusicVolume(value);
                Apply(save);
            });

            _soundsVolume.onValueChanged.Listen(screenLifetime, value => {
                save.SetSoundsVolume(value);
                Apply(save);
            });

            _shakeIntensity.onValueChanged.Listen(screenLifetime, value => {
                save.SetShakeIntensity(value);
                Apply(save);
            });

            _vSync.Bind(screenLifetime, save.VSync, value => {
                save.SetVSync(value);
                Apply(save);
            });

            _backButton.ListenClick(screenLifetime, () => completion.TrySetResult());

            await completion.Task;

            screenLifetime.Terminate();
            _gameObject.SetActive(false);
        }

        private void Apply(SettingsSave save)
        {
            // A copy goes out, so the screen keeps editing only its own instance.
            _settings.Apply(save.Copy()).Forget();
        }
    }
}
