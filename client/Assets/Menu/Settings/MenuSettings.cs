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
    /// Экран настроек в меню: кнопок применения нет, каждое изменение сразу уезжает в сейв.
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
        /// Копия сейва живёт ровно столько, сколько открыт экран: значения в неё пишутся
        /// на каждое изменение и тут же уходят в <see cref="ISettings.Apply"/>.
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
                save.MasterVolume = value;
                Apply(save);
            });

            _musicVolume.onValueChanged.Listen(lifetime, value => {
                save.MusicVolume = value;
                Apply(save);
            });

            _soundsVolume.onValueChanged.Listen(lifetime, value => {
                save.SoundsVolume = value;
                Apply(save);
            });

            _shakeIntensity.onValueChanged.Listen(lifetime, value => {
                save.ShakeIntensity = value;
                Apply(save);
            });

            _vSync.Bind(lifetime, save.VSync, value => {
                save.VSync = value;
                Apply(save);
            });

            return UniTask.CompletedTask;
        }

        private void Apply(SettingsSave save)
        {
            // Копия уходит наружу, чтобы экран продолжал править только свой экземпляр.
            _settings.Apply(save.Copy()).Forget();
        }
    }
}