using System;
using Internal;
using UnityEngine.UIElements;

namespace Global.UI.Toolkit
{
    public static class UIToolkitExtensions
    {
        public static void ListenClick(
            this Button button,
            IReadOnlyLifetime lifetime,
            Action callback) {
            button.clicked += callback;
            lifetime.Listen(() => button.clicked -= callback);
        }


        public static void ListenSubmit(
            this TextField field,
            IReadOnlyLifetime lifetime,
            Action<string> callback) {
            void Handler(KeyDownEvent evt) {
                if (evt.keyCode != UnityEngine.KeyCode.Return &&
                    evt.keyCode != UnityEngine.KeyCode.KeypadEnter)
                    return;

                var text = field.value;
                if (string.IsNullOrWhiteSpace(text))
                    return;

                field.value = string.Empty;
                callback(text);
            }

            field.RegisterCallback<KeyDownEvent>(Handler);
            lifetime.Listen(() => field.UnregisterCallback<KeyDownEvent>(Handler));
        }

        public static void BindText(
            this Label label,
            IReadOnlyLifetime lifetime,
            IViewableProperty<string> property) {
            property.View(lifetime, value => label.text = value);
        }

        public static void Show(this VisualElement element) {
            element.style.display = DisplayStyle.Flex;
        }

        public static void Hide(this VisualElement element) {
            element.style.display = DisplayStyle.None;
        }

        public static bool IsVisible(this VisualElement element) {
            return element.resolvedStyle.display == DisplayStyle.Flex;
        }
    }
}
