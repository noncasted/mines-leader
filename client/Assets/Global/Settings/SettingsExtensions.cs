using Internal;

namespace Global.Settings
{
    public static class SettingsExtensions
    {
        public static IScopeBuilder AddSettings(this IScopeBuilder builder)
        {
            var options = builder.GetAsset<SettingsOptions>();
            var view = builder.Instantiate(options.Prefab);

            builder.Register<Settings>()
                .WithAsset<SettingsOptions>()
                .WithParameter<ISettingsView>(view)
                .As<ISettings>()
                .As<IScopeSetupAsync>();

            return builder;
        }
    }
}