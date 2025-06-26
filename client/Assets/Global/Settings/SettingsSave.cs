namespace Global.Settings
{
    public class SettingsSave
    {
        public float MasterVolume { get; set; }
        public float SoundsVolume { get; set; }
        public float MusicVolume { get; set; }
        
        public float ShakeIntensity { get; set; }
        public bool VSync { get; set; }

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
    }
}