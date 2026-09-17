using UnityEngine;

namespace Internal
{
    public sealed class Sound
    {
        public Sound(AudioClip clip, float volume, AudioGroupAsset source, string name)
        {
            Clip = clip;
            _volume = volume;
            _source = source;
            _name = name;
        }

        private readonly float _volume;
        private readonly AudioGroupAsset _source;
        private readonly string _name;

        public AudioClip Clip { get; }

        // Громкость самого клипа из каталога, поверх неё играет громкость линии.
        // В редакторе читается из ассета группы: правка слайдера слышна без перезагрузки группы.
#if UNITY_EDITOR
        public float Volume => _source != null ? _source.GetVolume(_name, _volume) : _volume;
#else
        public float Volume => _volume;
#endif
    }
}
