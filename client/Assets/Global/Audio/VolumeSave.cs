using System;
using System.Collections.Generic;

namespace Global.Audio
{
    
    [Serializable]
    public class VolumeSave
    {
        public readonly Dictionary<AudioLine, float> Values = new()
        {
            { AudioLine.Music, 1 },
            { AudioLine.SFX, 1 }
        };
    }
}