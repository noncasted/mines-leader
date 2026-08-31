using System;

namespace Global.Audio
{
    [Serializable]
    public class VolumeSave
    {
        public float Music { get; set; } = 1f;
        public float SFX { get; set; } = 1f;
    }
}
