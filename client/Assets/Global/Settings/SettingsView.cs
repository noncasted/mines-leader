using System;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Global.Settings
{
    public enum SettingsViewResult
    {
        Apply,
        Cancel
    }

    public interface ISettingsView : IUIState
    {
    }

    public class SettingsView : MonoBehaviour, ISettingsView, IUIStateAsyncEnterHandler
    {
        [SerializeField] private DesignButton _applyButton;
        [SerializeField] private DesignButton _cancelButton;

        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _soundsVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;

        [SerializeField] private Slider _shakeIntensitySlider;

        [SerializeField] private DesignGroupSelection _vsyncSelection;

        private ISettings _settings;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        internal void Construct(ISettings settings)
        {
            _settings = settings;
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);

            var lifetime = handle.InnerLifetime;
            var saveCopy = _settings.Copy();

            _masterVolumeSlider.value = saveCopy.MasterVolume;
            _soundsVolumeSlider.value = saveCopy.SoundsVolume;
            _musicVolumeSlider.value = saveCopy.MusicVolume;

            _shakeIntensitySlider.value = saveCopy.ShakeIntensity;
            _vsyncSelection.Set(saveCopy.VSync ? SelectionGroupValue.On : SelectionGroupValue.Off);

            var completion = new UniTaskCompletionSource<SettingsViewResult>();

            lifetime.Listen(() => completion.TrySetResult(SettingsViewResult.Cancel));

            _applyButton.ListenClick(lifetime, () => completion.TrySetResult(SettingsViewResult.Apply));
            _cancelButton.ListenClick(lifetime, () => completion.TrySetResult(SettingsViewResult.Cancel));

            _masterVolumeSlider.onValueChanged.Listen(lifetime, Push);
            _soundsVolumeSlider.onValueChanged.Listen(lifetime, Push);
            _musicVolumeSlider.onValueChanged.Listen(lifetime, Push);

            _shakeIntensitySlider.onValueChanged.Listen(lifetime, value => saveCopy.ShakeIntensity = value);
            _vsyncSelection.Value.Advise(lifetime, value => saveCopy.VSync = value == SelectionGroupValue.On);

            var result = await completion.Task;

            switch (result)
            {
                case SettingsViewResult.Apply:
                    await _settings.Apply(saveCopy);
                    break;
                case SettingsViewResult.Cancel:
                    _settings.Revert();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return;

            void Push()
            {
                saveCopy.MasterVolume = _masterVolumeSlider.value;
                saveCopy.SoundsVolume = _soundsVolumeSlider.value;
                saveCopy.MusicVolume = _musicVolumeSlider.value;

                _settings.Push(saveCopy);
            }
        }
    }
}
