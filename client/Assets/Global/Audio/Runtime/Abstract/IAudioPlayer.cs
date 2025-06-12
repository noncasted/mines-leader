using UnityEngine;

namespace Global.Audio
{
    public interface IAudioPlayer
    {
        void PlaySound(AudioClip clip);
        void PlayLoopMusic(AudioClip clip);
    }
}