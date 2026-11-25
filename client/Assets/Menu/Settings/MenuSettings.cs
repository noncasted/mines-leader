using Cysharp.Threading.Tasks;
using Global.Settings;
using Global.UI;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Settings
{
    public interface IMenuSettings : IUIState
    {
    }

    /// <summary>
    /// The settings screen in the menu: there are no apply buttons, every change goes to the save at once.
    /// </summary>
    public class MenuSettings : IMenuSettings, IUIStateAsyncEnterHandler
    {
        public MenuSettings(ISettings settings, MenuSettingsBindings bindings)
        {
            _settings = settings;
            _masterVolume = bindings.Content.Master.Slider.Slider;
            _musicVolume = bindings.Content.Music.Slider.Slider;
            _soundsVolume = bindings.Content.Sounds.Slider.Slider;
            _shakeIntensity = bindings.Content.Shake.Slider.Slider;
            _vSync = bindings.Content.Vsync.Setting.MenuSettingsSwitch;
            _gameObject = bindings.GameObject;

            _gameObject.SetActive(false);
        }

        private readonly Slider _masterVolume;
        private readonly Slider _musicVolume;
        private readonly Slider _soundsVolume;
        private readonly Slider _shakeIntensity;
        private readonly MenuSettingsSwitch _vSync;
        private readonly GameObject _gameObject;

        private readonly ISettings _settings;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        /// <summary>
        /// The save copy lives exactly as long as the screen is open: values are written into it
        /// on every change and immediately go to <see cref="ISettings.Apply"/>.
        /// </summary>
        public UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(_gameObject);

            var lifetime = handle.InnerLifetime;
            var save = _settings.Copy();

            _masterVolume.SetValueWithoutNotify(save.MasterVolume);
            _musicVolume.SetValueWithoutNotify(save.MusicVolume);
            _soundsVolume.SetValueWithoutNotify(save.SoundsVolume);
            _shakeIntensity.SetValueWithoutNotify(save.ShakeIntensity);

            _masterVolume.onValueChanged.Listen(lifetime, value => {
                save.SetMasterVolume(value);
                Apply(save);
            });

            _musicVolume.onValueChanged.Listen(lifetime, value => {
                save.SetMusicVolume(value);
                Apply(save);
            });

            _soundsVolume.onValueChanged.Listen(lifetime, value => {
                save.SetSoundsVolume(value);
                Apply(save);
            });

            _shakeIntensity.onValueChanged.Listen(lifetime, value => {
                save.SetShakeIntensity(value);
                Apply(save);
            });

            _vSync.Bind(lifetime, save.VSync, value => {
                save.SetVSync(value);
                Apply(save);
            });

            return UniTask.CompletedTask;
        }

        private void Apply(SettingsSave save)
        {
            // A copy goes out, so the screen keeps editing only its own instance.
            _settings.Apply(save.Copy()).Forget();
        }
    }
}