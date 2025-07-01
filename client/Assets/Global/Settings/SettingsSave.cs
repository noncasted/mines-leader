using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Global.Settings
{
    [Serializable]
    public class SettingsSave
    {
        [field: SerializeField] public float MasterVolume { get; set; }
        [field: SerializeField] public float SoundsVolume { get; set; }
        [field: SerializeField] public float MusicVolume { get; set; }

        [field: SerializeField] public float ShakeIntensity { get; set; }
        [field: SerializeField] public bool VSync { get; set; }
        [field: SerializeField] public bool WasChanged { get; set; }

        public SettingsSave Copy()
        {
            return new SettingsSave
            {
                MasterVolume = MasterVolume,
                SoundsVolume = SoundsVolume,
                MusicVolume = MusicVolume,
                ShakeIntensity = ShakeIntensity,
                VSync = VSync
            };
        }
        
        public void CopyFrom(SettingsSave target)
        {
            MasterVolume = target.MasterVolume;
            SoundsVolume = target.SoundsVolume;
            MusicVolume = target.MusicVolume;
            ShakeIntensity = target.ShakeIntensity;
            VSync = target.VSync;
            WasChanged = true;
        }
    }
}