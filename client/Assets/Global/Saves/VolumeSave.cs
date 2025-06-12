using System;
using System.Collections.Generic;
using Global.Audio;
using Global.Publisher;

namespace Global.Saves
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
    
    public class VolumeSaveSerializer : StorageEntrySerializer<VolumeSave>
    {
        public VolumeSaveSerializer() : base("sound", new VolumeSave())
        {
        }
    }
}