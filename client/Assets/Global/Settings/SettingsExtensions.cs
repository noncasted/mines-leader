using Internal;

namespace Global.Settings
{
    public static class SettingsExtensions
    {
        public static IScopeBuilder AddSettings(this IScopeBuilder builder)
        {
            builder.Register<Settings>()
                   .WithAsset<SettingsOptions>()
                   .As<ISettings>()
                   .As<IScopeSetupAsync>();

            var view = builder.Instantiate(builder.GetAsset<SettingsOptions>().ViewPrefab);

            builder.RegisterComponent(view)
                   .As<ISettingsView>();

            return builder;
        }
    }
}