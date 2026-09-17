using System.Collections.Generic;
using Global.Publisher;
using Global.Settings;
using Internal;
using UnityEngine;

namespace Global.Audio
{
    public interface IAudioPlayer
    {
        void PlaySound(Sound sound);
        void PlayLoopMusic(Sound sound);
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
        private float _musicScale = 1f;
        private float[] _soundScales;

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

            _soundScales = new float[_soundSources.Length];

            for (var i = 0; i < _soundScales.Length; i++)
                _soundScales[i] = 1f;

            // Общие звуки качаются параллельно старту: до их загрузки кнопки молчат.
            ButtonSounds.Clicked = () =>
            {
                if (GlobalAudio.IsLoaded == true)
                    this.PlayRandomFromGroup(GlobalAudio.UIButtonClick);
            };

            ButtonSounds.Hovered = () =>
            {
                if (GlobalAudio.IsLoaded == true)
                    this.PlayRandomFromGroup(GlobalAudio.UIElementHover);
            };

            lifetime.Listen(() =>
            {
                ButtonSounds.Clicked = null;
                ButtonSounds.Hovered = null;
            });
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
            _musicSource.volume = _values[AudioLine.Music] * _musicScale;

            for (var i = 0; i < _soundSources.Length; i++)
                _soundSources[i].volume = _values[AudioLine.SFX] * _soundScales[i];
        }

        public void PlaySound(Sound sound)
        {
            var index = 0;

            for (var i = 0; i < _soundSources.Length; i++)
            {
                if (_soundSources[i].isPlaying == true)
                    continue;

                index = i;
                break;
            }

            var source = _soundSources[index];
            _soundScales[index] = sound.Volume;
            source.clip = sound.Clip;

            if (_isMuted.Value == false)
                source.volume = _values[AudioLine.SFX] * sound.Volume;

            source.Play();
        }

        public void PlayLoopMusic(Sound sound)
        {
            // Меню зовёт музыку на каждом входе: уже играющий трек не перезапускается.
            if (_musicSource.isPlaying == true && _musicSource.clip == sound.Clip)
                return;

            _musicScale = sound.Volume;
            _musicSource.loop = true;
            _musicSource.clip = sound.Clip;

            if (_isMuted.Value == false)
                _musicSource.volume = _values[AudioLine.Music] * sound.Volume;

            _musicSource.Play();
        }
    }
}