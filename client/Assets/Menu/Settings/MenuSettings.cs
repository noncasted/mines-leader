using Cysharp.Threading.Tasks;
using Global.Settings;
using Global.UI;
using Internal;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Menu.Settings
{
    public interface IMenuSettings : IUIState
    {
    }

    /// <summary>
    /// Экран настроек в меню: кнопок применения нет, каждое изменение сразу уезжает в сейв.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSettings : MonoBehaviour, IMenuSettings, ISceneService, IUIStateAsyncEnterHandler
    {
        [SerializeField] private Slider _masterVolume;
        [SerializeField] private Slider _musicVolume;
        [SerializeField] private Slider _soundsVolume;
        [SerializeField] private Slider _shakeIntensity;
        [SerializeField] private MenuSettingsSwitch _vSync;

        private ISettings _settings;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        internal void Construct(ISettings settings)
        {
            _settings = settings;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuSettings>();
        }

        /// <summary>
        /// Копия сейва живёт ровно столько, сколько открыт экран: значения в неё пишутся
        /// на каждое изменение и тут же уходят в <see cref="ISettings.Apply"/>.
        /// </summary>
        public UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);

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
