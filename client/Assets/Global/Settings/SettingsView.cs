using System;
using Cysharp.Threading.Tasks;
using Global.UI.Toolkit;
using Internal;
using Tools.PrefabBuilder;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Global.Settings {
    public enum SettingsViewResult {
        Apply,
        Cancel
    }

    public interface ISettingsView {
        UniTask<SettingsViewResult> Show(SettingsSave data, Action pushCallback);
    }

    public class SettingsView : ISettingsView {
        public async UniTask<SettingsViewResult> Show(SettingsSave data, Action pushCallback) {
            var prefab = (GameObject)Object.Instantiate(Prefabs.SettingsPanel);
            var document = prefab.GetComponent<UIDocument>();
            var root = document.rootVisualElement;

            if (root == null) {
                Object.Destroy(prefab);
                return SettingsViewResult.Cancel;
            }

            var lifetime = new Lifetime();
            var completionSource = new UniTaskCompletionSource<SettingsViewResult>();

            var sliderMaster = root.Q<Slider>("slider-master");
            var sliderSounds = root.Q<Slider>("slider-sounds");
            var sliderMusic = root.Q<Slider>("slider-music");
            var sliderShake = root.Q<Slider>("slider-shake");
            var btnVsyncOn = root.Q<Button>("btn-vsync-on");
            var btnVsyncOff = root.Q<Button>("btn-vsync-off");
            var btnCancel = root.Q<NavButton>("btn-cancel");
            var btnApply = root.Q<NavButton>("btn-apply");

            sliderMaster.value = data.MasterVolume;
            sliderSounds.value = data.SoundsVolume;
            sliderMusic.value = data.MusicVolume;
            sliderShake.value = data.ShakeIntensity;

            void RefreshVsync(bool vsync) {
                if (vsync) {
                    btnVsyncOn.AddToClassList("active");
                    btnVsyncOff.RemoveFromClassList("active");
                } else {
                    btnVsyncOn.RemoveFromClassList("active");
                    btnVsyncOff.AddToClassList("active");
                }
            }
            RefreshVsync(data.VSync);

            EventCallback<ChangeEvent<float>> onMaster = evt => { data.MasterVolume = evt.newValue; pushCallback?.Invoke(); };
            EventCallback<ChangeEvent<float>> onSounds = evt => { data.SoundsVolume = evt.newValue; pushCallback?.Invoke(); };
            EventCallback<ChangeEvent<float>> onMusic = evt => { data.MusicVolume = evt.newValue; pushCallback?.Invoke(); };
            EventCallback<ChangeEvent<float>> onShake = evt => { data.ShakeIntensity = evt.newValue; };

            sliderMaster.RegisterValueChangedCallback(onMaster);
            sliderSounds.RegisterValueChangedCallback(onSounds);
            sliderMusic.RegisterValueChangedCallback(onMusic);
            sliderShake.RegisterValueChangedCallback(onShake);

            btnVsyncOn.RegisterCallback<ClickEvent>(_ => { data.VSync = true; RefreshVsync(true); });
            btnVsyncOff.RegisterCallback<ClickEvent>(_ => { data.VSync = false; RefreshVsync(false); });

            btnCancel.ListenClick(lifetime, () => completionSource.TrySetResult(SettingsViewResult.Cancel));
            btnApply.ListenClick(lifetime, () => completionSource.TrySetResult(SettingsViewResult.Apply));

            var result = await completionSource.Task;

            lifetime.Terminate();
            Object.Destroy(prefab);

            return result;
        }
    }
}
