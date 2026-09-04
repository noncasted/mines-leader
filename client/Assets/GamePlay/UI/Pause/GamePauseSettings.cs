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
        /// Открывает настройки поверх паузы и ждёт, пока игрок нажмёт «назад».
        /// </summary>
        UniTask Process(IReadOnlyLifetime lifetime);
    }

    /// <summary>
    /// Экран настроек в паузе: кнопок применения нет, каждое изменение сразу уезжает в сейв.
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
        /// Копия сейва живёт ровно столько, сколько открыт экран: значения в неё пишутся
        /// на каждое изменение и тут же уходят в <see cref="ISettings.Apply"/>.
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
                save.MasterVolume = value;
                Apply(save);
            });

            _musicVolume.onValueChanged.Listen(screenLifetime, value => {
                save.MusicVolume = value;
                Apply(save);
            });

            _soundsVolume.onValueChanged.Listen(screenLifetime, value => {
                save.SoundsVolume = value;
                Apply(save);
            });

            _shakeIntensity.onValueChanged.Listen(screenLifetime, value => {
                save.ShakeIntensity = value;
                Apply(save);
            });

            _vSync.Bind(screenLifetime, save.VSync, value => {
                save.VSync = value;
                Apply(save);
            });

            _backButton.ListenClick(screenLifetime, () => completion.TrySetResult());

            await completion.Task;

            screenLifetime.Terminate();
            _gameObject.SetActive(false);
        }

        private void Apply(SettingsSave save)
        {
            // Копия уходит наружу, чтобы экран продолжал править только свой экземпляр.
            _settings.Apply(save.Copy()).Forget();
        }
    }
}
