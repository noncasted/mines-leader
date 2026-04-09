using Internal;
using Tools;

namespace Global.Settings
{
    public static class SettingsExtensions
    {
        public static IScopeBuilder AddSettings(this IScopeBuilder builder)
        {
            var view = builder.Instantiate(Prefabs.Settings.As<SettingsView>());

            builder.Register<Settings>()
                   .WithAsset<SettingsOptions>()
                   .WithParameter<ISettingsView>(view)
                   .As<ISettings>()
                   .As<IScopeSetupAsync>();

            return builder;
        }
    }
}