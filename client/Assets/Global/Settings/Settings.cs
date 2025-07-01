using System;
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

        void Open();
    }

    public class Settings : ISettings, IScopeSetupAsync
    {
        public Settings(ISaves saves, ISettingsView view, SettingsOptions options)
        {
            _saves = saves;
            _view = view;
            _options = options;
        }

        private readonly ISaves _saves;
        private readonly ISettingsView _view;
        private readonly SettingsOptions _options;

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

        public async UniTask OnSetupAsync(IReadOnlyLifetime lifetime)
        {
            _save = _saves.Get<SettingsSave>();

            if (_save.WasChanged == false)
                _save.CopyFrom(_options.DefaultValues);
            
            PushValues(_save);
        }

        public void Open()
        {
            Process().Forget();
        }

        private async UniTask Process()
        {
            var saveCopy = _save.Copy();

            var result = await _view.Show(saveCopy, () => PushValues(saveCopy));

            switch (result)
            {
                case SettingsViewResult.Apply:
                    saveCopy.WasChanged = true;
                    await _saves.Save(saveCopy);
                    _save = saveCopy;
                    PushValues(saveCopy);
                    break;
                case SettingsViewResult.Cancel:
                    PushValues(_save);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void PushValues(SettingsSave save)
        {
            _masterVolume.Set(save.MasterVolume);
            _soundsVolume.Set(save.SoundsVolume);
            _musicVolume.Set(save.MusicVolume);

            _shakeIntensity.Set(save.ShakeIntensity);

            _vSync.Set(save.VSync);
        }
    }
}