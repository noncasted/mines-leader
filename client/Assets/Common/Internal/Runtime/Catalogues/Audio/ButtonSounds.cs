using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Internal
{
    // Internal не видит аудиоплеер Global, поэтому кнопки сообщают о клике и ховере через хуки,
    // а плеер их выставляет. Эмиттер вешается на кнопку в ListenClick — это общий вход всех кнопок.
    public static class ButtonSounds
    {
        public static Action Clicked { get; set; }
        public static Action Hovered { get; set; }

        public static void Attach(Button button)
        {
            if (button.TryGetComponent(out ButtonSoundEmitter _) == false)
                button.gameObject.AddComponent<ButtonSoundEmitter>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class ButtonSoundEmitter : MonoBehaviour, IPointerEnterHandler
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button.IsInteractable() == true)
                ButtonSounds.Hovered?.Invoke();
        }

        private static void OnClicked()
        {
            ButtonSounds.Clicked?.Invoke();
        }
    }
}
