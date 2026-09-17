using System.Collections.Generic;
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
    public class AudioPlayer : MonoBehaviour, IAudioVolume, IAudioPlayer, IScopeSetupCompletion
    {
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource[] _soundSources;

        private ISettings _settings;
        private float _musicScale = 1f;
        private float[] _soundScales;

        private readonly Dictionary<AudioLine, float> _values = new();
        private readonly ViewableProperty<bool> _isMuted = new();

        public IReadOnlyDictionary<AudioLine, float> Values => _values;
        public IViewableProperty<bool> IsMuted => _isMuted;

        [Inject]
        internal void Construct(ISettings settings)
        {
            _settings = settings;
        }

        // Громкость берётся из ISettings после их setup: в свежем сейве её ещё нет,
        // значения по умолчанию подставляет Settings. Слайдеры настроек меняют её на лету.
        public void OnSetupCompletion(IReadOnlyLifetime lifetime)
        {
            _soundScales = new float[_soundSources.Length];

            for (var i = 0; i < _soundScales.Length; i++)
                _soundScales[i] = 1f;

            _settings.MasterVolume.View(lifetime, UpdateVolume);
            _settings.MusicVolume.View(lifetime, UpdateVolume);
            _settings.SoundsVolume.View(lifetime, UpdateVolume);

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

        private void UpdateVolume()
        {
            var master = _settings.MasterVolume.Value;

            // Обе линии пишутся до ApplyVolume: он читает их вместе.
            _values[AudioLine.Music] = _settings.MusicVolume.Value * master;
            _values[AudioLine.SFX] = _settings.SoundsVolume.Value * master;

            if (_isMuted.Value == false)
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