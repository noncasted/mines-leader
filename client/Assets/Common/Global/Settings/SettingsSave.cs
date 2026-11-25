using System;
using UnityEngine;

namespace Global.Settings
{
    // The saves (ItchSaves, JsonUtility) and SettingsOptions.asset keep the backing fields of the properties
    // ("<MasterVolume>k__BackingField"): a rename of a property loses the value in the saves of the players.
    [Serializable]
    public class SettingsSave
    {
        // ISaves creates an empty save to read into it (Get<T> needs new()).
        public SettingsSave()
        {
        }

        public SettingsSave(float masterVolume, float soundsVolume, float musicVolume, float shakeIntensity, bool vSync)
        {
            MasterVolume = masterVolume;
            SoundsVolume = soundsVolume;
            MusicVolume = musicVolume;
            ShakeIntensity = shakeIntensity;
            VSync = vSync;
        }

        [field: SerializeField] public float MasterVolume { get; private set; }
        [field: SerializeField] public float SoundsVolume { get; private set; }
        [field: SerializeField] public float MusicVolume { get; private set; }

        [field: SerializeField] public float ShakeIntensity { get; private set; }
        [field: SerializeField] public bool VSync { get; private set; }
        [field: SerializeField] public bool WasChanged { get; private set; }

        public void SetMasterVolume(float value)
        {
            MasterVolume = value;
        }

        public void SetSoundsVolume(float value)
        {
            SoundsVolume = value;
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = value;
        }

        public void SetShakeIntensity(float value)
        {
            ShakeIntensity = value;
        }

        public void SetVSync(bool value)
        {
            VSync = value;
        }

        public void MarkChanged()
        {
            WasChanged = true;
        }

        // WasChanged is not copied: Apply marks the save it stores.
        public SettingsSave Copy()
        {
            return new SettingsSave(MasterVolume, SoundsVolume, MusicVolume, ShakeIntensity, VSync);
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
