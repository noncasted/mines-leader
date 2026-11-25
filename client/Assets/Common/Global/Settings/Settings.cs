using Cysharp.Threading.Tasks;
using Global.Audio;
using Global.Publisher;
using Internal;

namespace Global.Settings
{
    public interface ISettings : IAudioVolume
    {
        IViewableProperty<float> ShakeIntensity { get; }
        IViewableProperty<bool> VSync { get; }

        SettingsSave Copy();
        void Push(SettingsSave save);
        UniTask Apply(SettingsSave save);
    }

    public class Settings : ISettings
    {
        // The saves are loaded by AddSaves before the container: the values are ready from the constructor, so the
        // audio and the other readers never see the defaults first.
        public Settings(ISaves saves)
        {
            _saves = saves;

            _save = _saves.Get<SettingsSave>();

            if (_save.WasChanged == false)
                _save.CopyFrom(GlobalAssets.SettingsOptions.DefaultValues);

            Push(_save);
        }

        private readonly ISaves _saves;

        private readonly ViewableProperty<float> _masterVolume = new();
        private readonly ViewableProperty<float> _soundsVolume = new();
        private readonly ViewableProperty<float> _musicVolume = new();
        private readonly ViewableProperty<float> _shakeIntensity = new();
        private readonly ViewableProperty<bool> _vSync = new();

        private SettingsSave _save;

        public IViewableProperty<float> MasterVolume => _masterVolume;
        public IViewableProperty<float> SoundsVolume => _soundsVolume;
        public IViewableProperty<float> MusicVolume => _musicVolume;
        public IViewableProperty<float> ShakeIntensity => _shakeIntensity;
        public IViewableProperty<bool> VSync => _vSync;

        public SettingsSave Copy()
        {
            return _save.Copy();
        }

        public void Push(SettingsSave save)
        {
            _masterVolume.Set(save.MasterVolume);
            _soundsVolume.Set(save.SoundsVolume);
            _musicVolume.Set(save.MusicVolume);

            _shakeIntensity.Set(save.ShakeIntensity);

            _vSync.Set(save.VSync);
        }

        public async UniTask Apply(SettingsSave save)
        {
            save.MarkChanged();
            await _saves.Save(save);
            _save = save;
            Push(save);
        }
    }
}