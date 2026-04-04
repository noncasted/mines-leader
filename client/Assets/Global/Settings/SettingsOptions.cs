using Internal;
using UnityEngine;

namespace Global.Settings
{
    public class SettingsOptions : EnvAsset
    {
        [SerializeField] private SettingsSave _defaultValues = new SettingsSave
        {
            MasterVolume = 1f,
            SoundsVolume = 1f,
            MusicVolume = 1f,
            ShakeIntensity = 0.5f,
            VSync = true
        };

        public SettingsSave DefaultValues => _defaultValues;
    }
}