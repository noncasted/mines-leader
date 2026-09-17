using Internal;

namespace Global.Audio
{
    public static class GlobalAudioExtensions
    {
        public static IScopeBuilder AddAudio(this IScopeBuilder builder)
        {
            // Регистрируется инстанс: источники на самом ассете префаба не играют.
            var player = builder.Instantiate(GlobalPrefabs.GlobalAudioPlayer);
            var listener = builder.Instantiate(GlobalPrefabs.GlobalAudioListener);

            builder.RegisterComponent(player)
                   .As<IAudioVolume>()
                   .As<IAudioPlayer>()
                   .As<IScopeSetupCompletion>();

            builder.RegisterComponent(listener)
                   .As<IAudioListener>()
                   .As<IScopeBaseSetup>();

            return builder;
        }
    }
}