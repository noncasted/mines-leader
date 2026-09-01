using Cysharp.Threading.Tasks;
using Global.Settings;
using Internal;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

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
    [DisallowMultipleComponent]
    public class GamePauseSettings : MonoBehaviour, IGamePauseSettings, ISceneService
    {
        [SerializeField] private Slider _masterVolume;
        [SerializeField] private Slider _musicVolume;
        [SerializeField] private Slider _soundsVolume;
        [SerializeField] private Slider _shakeIntensity;
        [SerializeField] private GamePauseSettingsSwitch _vSync;
        [SerializeField] private Button _backButton;

        private ISettings _settings;

        [Inject]
        internal void Construct(ISettings settings)
        {
            _settings = settings;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IGamePauseSettings>();
        }

        /// <summary>
        /// Копия сейва живёт ровно столько, сколько открыт экран: значения в неё пишутся
        /// на каждое изменение и тут же уходят в <see cref="ISettings.Apply"/>.
        /// </summary>
        public async UniTask Process(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(true);

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
            gameObject.SetActive(false);
        }

        private void Apply(SettingsSave save)
        {
            // Копия уходит наружу, чтобы экран продолжал править только свой экземпляр.
            _settings.Apply(save.Copy()).Forget();
        }
    }
}
