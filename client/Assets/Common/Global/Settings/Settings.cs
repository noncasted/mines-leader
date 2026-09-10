using Cysharp.Threading.Tasks;
using Global.Publisher;
using Internal;

namespace Global.Settings
{
    public interface ISettings
    {
        IViewableProperty<float> MasterVolume { get; }
        IViewableProperty<float> SoundsVolume { get; }
        IViewableProperty<float> MusicVolume { get; }

        IViewableProperty<float> ShakeIntensity { get; }
        IViewableProperty<bool> VSync { get; }

        SettingsSave Copy();
        void Push(SettingsSave save);
        UniTask Apply(SettingsSave save);
        void Revert();
    }

    public class Settings : ISettings, IScopeSetupAsync
    {
        public Settings(ISaves saves)
        {
            _saves = saves;
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

        public UniTask OnSetupAsync(IReadOnlyLifetime lifetime)
        {
            var options = GlobalAssets.SettingsOptions;

            _save = _saves.Get<SettingsSave>();

            if (_save.WasChanged == false)
                _save.CopyFrom(options.DefaultValues);

            Push(_save);

            return UniTask.CompletedTask;
        }

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
            save.WasChanged = true;
            await _saves.Save(save);
            _save = save;
            Push(save);
        }

        public void Revert()
        {
            Push(_save);
        }
    }
}