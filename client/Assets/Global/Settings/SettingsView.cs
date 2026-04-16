using System;
using Cysharp.Threading.Tasks;
using Global.UI.Toolkit;
using Internal;
using UnityEngine;
using UnityEngine.UIElements;

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
            var doc = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            if (doc == null) return SettingsViewResult.Cancel;

            var container = doc.rootVisualElement.Q<VisualElement>("bottom-bar-root");
            if (container == null) return SettingsViewResult.Cancel;

            var lifetime = new Lifetime();
            var completionSource = new UniTaskCompletionSource<SettingsViewResult>();

            var overlay = BuildOverlay(data, pushCallback, lifetime, completionSource);
            container.Add(overlay);

            var result = await completionSource.Task;

            lifetime.Terminate();
            container.Remove(overlay);

            return result;
        }

        private VisualElement BuildOverlay(
            SettingsSave data,
            Action pushCallback,
            IReadOnlyLifetime lifetime,
            UniTaskCompletionSource<SettingsViewResult> completionSource) {
            var overlay = new VisualElement();
            overlay.AddToClassList("settings-overlay");

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("settings-panel");

            var panelMid = new VisualElement();
            panelMid.AddToClassList("panel-mid");

            panelMid.Add(CreateDiv("panel-highlight"));
            panelMid.Add(CreateDiv("panel-header-upper"));
            panelMid.Add(CreateDiv("panel-header-lower"));

            var body = new VisualElement();
            body.AddToClassList("panel-body");

            var title = new Label("Settings");
            title.AddToClassList("settings-title");
            body.Add(title);
            body.Add(CreateDivider());

            body.Add(CreateSectionLabel("Audio"));

            body.Add(CreateSliderRow("Master Volume", data.MasterVolume, lifetime, value => {
                data.MasterVolume = value;
                pushCallback?.Invoke();
            }));

            body.Add(CreateSliderRow("Sound Effects", data.SoundsVolume, lifetime, value => {
                data.SoundsVolume = value;
                pushCallback?.Invoke();
            }));

            body.Add(CreateSliderRow("Music", data.MusicVolume, lifetime, value => {
                data.MusicVolume = value;
                pushCallback?.Invoke();
            }));

            body.Add(CreateDivider());
            body.Add(CreateSectionLabel("Effects"));

            body.Add(CreateSliderRow("Shake", data.ShakeIntensity, lifetime, value => {
                data.ShakeIntensity = value;
            }));

            body.Add(CreateDivider());
            body.Add(CreateSectionLabel("Video"));

            body.Add(CreateVSyncRow(data, lifetime));

            var buttonsRow = new VisualElement();
            buttonsRow.AddToClassList("settings-buttons");

            var cancelBtn = new NavButton { text = "Cancel" };
            cancelBtn.ListenClick(lifetime, () => completionSource.TrySetResult(SettingsViewResult.Cancel));

            var separator = new VisualElement();
            separator.AddToClassList("separator");

            var applyBtn = new NavButton { text = "Apply" };
            applyBtn.ListenClick(lifetime, () => completionSource.TrySetResult(SettingsViewResult.Apply));

            buttonsRow.Add(cancelBtn);
            buttonsRow.Add(separator);
            buttonsRow.Add(applyBtn);
            body.Add(buttonsRow);

            panelMid.Add(body);
            panel.Add(panelMid);
            overlay.Add(panel);

            return overlay;
        }

        private VisualElement CreateSliderRow(string label, float initialValue, IReadOnlyLifetime lifetime, Action<float> onChange) {
            var row = new VisualElement();
            row.AddToClassList("settings-row");

            var lbl = new Label(label);
            lbl.AddToClassList("settings-row-label");

            var slider = new Slider(0f, 1f) { value = initialValue };
            slider.AddToClassList("settings-slider");

            EventCallback<ChangeEvent<float>> handler = evt => onChange(evt.newValue);
            slider.RegisterValueChangedCallback(handler);
            lifetime.Listen(() => slider.UnregisterValueChangedCallback(handler));

            row.Add(lbl);
            row.Add(slider);

            return row;
        }

        private VisualElement CreateVSyncRow(SettingsSave data, IReadOnlyLifetime lifetime) {
            var row = new VisualElement();
            row.AddToClassList("settings-row");

            var lbl = new Label("VSync");
            lbl.AddToClassList("settings-row-label");

            var group = new VisualElement();
            group.AddToClassList("settings-toggle-group");

            var onBtn = new Button { text = "On" };
            onBtn.AddToClassList("settings-toggle-btn");

            var offBtn = new Button { text = "Off" };
            offBtn.AddToClassList("settings-toggle-btn");

            void Refresh(bool vsync) {
                if (vsync) {
                    onBtn.AddToClassList("active");
                    offBtn.RemoveFromClassList("active");
                } else {
                    onBtn.RemoveFromClassList("active");
                    offBtn.AddToClassList("active");
                }
            }

            Refresh(data.VSync);

            onBtn.ListenClick(lifetime, () => { data.VSync = true; Refresh(true); });
            offBtn.ListenClick(lifetime, () => { data.VSync = false; Refresh(false); });

            group.Add(onBtn);
            group.Add(offBtn);

            row.Add(lbl);
            row.Add(group);

            return row;
        }

        private static VisualElement CreateDivider() {
            var divider = new VisualElement();
            divider.AddToClassList("settings-divider");
            return divider;
        }

        private static Label CreateSectionLabel(string text) {
            var label = new Label(text);
            label.AddToClassList("settings-section-label");
            return label;
        }

        private static VisualElement CreateDiv(string className) {
            var div = new VisualElement();
            div.AddToClassList(className);
            return div;
        }
    }
}
