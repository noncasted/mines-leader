using System;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace Global.Settings
{
    public enum SettingsViewResult
    {
        Apply,
        Cancel
    }

    public interface ISettingsView
    {
        UniTask<SettingsViewResult> Show(SettingsSave data, Action pushCallback);
    }

    public class SettingsView : MonoBehaviour, ISettingsView
    {
        [SerializeField] private DesignButton _applyButton;
        [SerializeField] private DesignButton _cancelButton;

        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _soundsVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;

        [SerializeField] private Slider _shakeIntensitySlider;

        [SerializeField] private DesignGroupSelection _vsyncSelection;

        public async UniTask<SettingsViewResult> Show(SettingsSave data, Action pushCallback)
        {
            gameObject.SetActive(true);

            var lifetime = this.GetObjectLifetime();

            _masterVolumeSlider.value = data.MasterVolume;
            _soundsVolumeSlider.value = data.SoundsVolume;
            _musicVolumeSlider.value = data.MusicVolume;

            _shakeIntensitySlider.value = data.ShakeIntensity;
            _vsyncSelection.Set(data.VSync ? SelectionGroupValue.On : SelectionGroupValue.Off);

            var completionSource = new UniTaskCompletionSource<SettingsViewResult>();

            _applyButton.ListenClick(lifetime, () => completionSource.TrySetResult(SettingsViewResult.Apply));
            _cancelButton.ListenClick(lifetime, () => completionSource.TrySetResult(SettingsViewResult.Cancel));

            _masterVolumeSlider.onValueChanged.Listen(lifetime, Push);
            _soundsVolumeSlider.onValueChanged.Listen(lifetime, Push);
            _musicVolumeSlider.onValueChanged.Listen(lifetime, Push);

            _shakeIntensitySlider.onValueChanged.Listen(lifetime, value => data.ShakeIntensity = value);
            _vsyncSelection.Value.Advise(lifetime, value => data.VSync = value == SelectionGroupValue.On);

            var result = await completionSource.Task;

            gameObject.SetActive(false);

            return result;

            void Push()
            {
                data.MasterVolume = _masterVolumeSlider.value;
                data.SoundsVolume = _soundsVolumeSlider.value;
                data.MusicVolume = _musicVolumeSlider.value;

                pushCallback?.Invoke();
            }
        }
    }
}