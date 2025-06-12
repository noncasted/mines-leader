using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Publisher;
using Global.Saves;
using Internal;
using UnityEngine;

namespace Global.Audio
{
    [DisallowMultipleComponent]
    public class AudioPlayer : MonoBehaviour, IAudioVolume, IAudioPlayer, IDataStorageLoadListener
    {
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource[] _soundSources;

        private IDataStorage _dataStorage;

        private readonly Dictionary<AudioLine, float> _values = new();
        private readonly ViewableProperty<bool> _isMuted = new();
        
        public IReadOnlyDictionary<AudioLine, float> Values => _values;
        public IViewableProperty<bool> IsMuted => _isMuted;

        public async UniTask OnDataStorageLoaded(IReadOnlyLifetime lifetime, IDataStorage dataStorage)
        {
            _dataStorage = dataStorage;
            var save = await _dataStorage.GetEntry<VolumeSave>();
            
            _values[AudioLine.Music] = save.Values[AudioLine.Music];
            _values[AudioLine.SFX] = save.Values[AudioLine.SFX];
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