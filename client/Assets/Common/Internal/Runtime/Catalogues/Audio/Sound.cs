using UnityEngine;

namespace Internal
{
    public sealed class Sound
    {
        public Sound(AudioClip clip, float volume)
        {
            Clip = clip;
            Volume = volume;
        }

        public AudioClip Clip { get; }

        // Громкость самого клипа из каталога, поверх неё играет громкость линии.
        public float Volume { get; }
    }
}
