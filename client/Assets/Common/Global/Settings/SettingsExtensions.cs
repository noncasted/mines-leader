using Global.Audio;
using Internal;

namespace Global.Settings
{
    public static class SettingsExtensions
    {
        public static IScopeBuilder AddSettings(this IScopeBuilder builder)
        {
            builder.Register<Settings>()
                   .As<ISettings>()
                   .As<IAudioVolume>()
                   .As<IScopeSetupAsync>();

            return builder;
        }
    }
}