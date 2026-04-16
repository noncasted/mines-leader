using Internal;

namespace Global.Settings {
    public static class SettingsExtensions {
        public static IScopeBuilder AddSettings(this IScopeBuilder builder) {
            builder.Register<Settings>()
                   .WithAsset<SettingsOptions>()
                   .WithParameter<ISettingsView>(new SettingsView())
                   .As<ISettings>()
                   .As<IScopeSetupAsync>();

            return builder;
        }
    }
}
