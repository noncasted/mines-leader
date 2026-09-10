using System.Collections.Generic;
using Global.Publisher;
using Global.Settings;
using Internal;
using UnityEngine;

namespace Global.Audio
{
    public interface IAudioPlayer
    {
        void PlaySound(AudioClip clip);
        void PlayLoopMusic(AudioClip clip);
    }
    
    public interface IAudioVolume
    {
        IReadOnlyDictionary<AudioLine, float> Values { get; }
        IViewableProperty<bool> IsMuted { get; }

        void Mute();
        void Unmute();
        void SetVolume(AudioLine line, float volume);
    }
    
    [DisallowMultipleComponent]
    public class AudioPlayer : MonoBehaviour, IAudioVolume, IAudioPlayer, IScopeSetup
    {
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource[] _soundSources;

        private ISaves _saves;

        private readonly Dictionary<AudioLine, float> _values = new();
        private readonly ViewableProperty<bool> _isMuted = new();

        public IReadOnlyDictionary<AudioLine, float> Values => _values;
        public IViewableProperty<bool> IsMuted => _isMuted;

        [Inject]
        internal void Construct(ISaves saves)
        {
            _saves = saves;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var save = _saves.Get<SettingsSave>();

            _values[AudioLine.Music] = save.MusicVolume * save.MasterVolume;
            _values[AudioLine.SFX] = save.SoundsVolume * save.MasterVolume;
        }

        public void Mute()
        {
            _musicSource.volume = 0f;

            foreach (var source in _soundSources)
                source.volume = 0f;

            _isMuted.Set(true);
        }

        public void Unmute()
        {
            _isMuted.Set(false);
            ApplyVolume();
        }

        public void SetVolume(AudioLine line, float volume)
        {
            _values[line] = volume;

            if (_isMuted.Value == true)
                return;

            ApplyVolume();
        }

        private void ApplyVolume()
        {
            _musicSource.volume = _values[AudioLine.Music];

            foreach (var source in _soundSources)
                source.volume = _values[AudioLine.SFX];
        }

        public void PlaySound(AudioClip clip)
        {
            foreach (var source in _soundSources)
            {
                if (source.isPlaying == true)
                    continue;

                source.clip = clip;
                source.Play();
                return;
            }

            _soundSources[0].clip = clip;
            _soundSources[0].Play();
        }

        public void PlayLoopMusic(AudioClip clip)
        {
            _musicSource.loop = true;
            _musicSource.clip = clip;
            _musicSource.Play();
        }
    }
}