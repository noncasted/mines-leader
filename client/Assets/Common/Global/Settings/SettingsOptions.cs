using Internal;
using UnityEngine;

namespace Global.Settings
{
    public class SettingsOptions : EnvAsset
    {
        [SerializeField]
        private SettingsSave _defaultValues = new(
            masterVolume: 1f,
            soundsVolume: 1f,
            musicVolume: 1f,
            shakeIntensity: 0.5f,
            vSync: true);

        public SettingsSave DefaultValues => _defaultValues;
    }
}