using System;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Settings
{
    /// <summary>
    /// A pair of On/Off buttons: the active half takes the <see cref="SettingsSprites.On"/> sprite,
    /// the other one - <see cref="SettingsSprites.Off"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSettingsSwitch : MonoBehaviour
    {
        [SerializeField] private Image _onPlate;
        [SerializeField] private Image _offPlate;
        [SerializeField] private Button _onButton;
        [SerializeField] private Button _offButton;

        private bool _value;

        public void Bind(IReadOnlyLifetime lifetime, bool value, Action<bool> changed)
        {
            Set(value);

            _onButton.ListenClick(lifetime, () => Switch(true, changed));
            _offButton.ListenClick(lifetime, () => Switch(false, changed));
        }

        public void Set(bool value)
        {
            _value = value;

            _onPlate.sprite = value == true ? Sprites.Settings.On : Sprites.Settings.Off;
            _offPlate.sprite = value == true ? Sprites.Settings.Off : Sprites.Settings.On;
        }

        private void Switch(bool value, Action<bool> changed)
        {
            if (_value == value)
                return;

            Set(value);
            changed.Invoke(value);
        }
    }
}
