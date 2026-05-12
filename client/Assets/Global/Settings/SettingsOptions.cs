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
        
        [SerializeField] private SettingsView _viewPrefab;

        public SettingsSave DefaultValues => _defaultValues;
        public SettingsView ViewPrefab => _viewPrefab;
    }
}