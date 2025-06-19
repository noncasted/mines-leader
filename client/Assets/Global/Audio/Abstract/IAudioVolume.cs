using System.Collections.Generic;
using Internal;

namespace Global.Audio
{
    public interface IAudioVolume
    {
        IReadOnlyDictionary<AudioLine, float> Values { get; }
        IViewableProperty<bool> IsMuted { get; }
        
        void Mute();
        void Unmute();
        void SetVolume(AudioLine line, float volume);
    }
}