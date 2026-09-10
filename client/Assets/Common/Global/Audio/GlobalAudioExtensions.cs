using Internal;

namespace Global.Audio
{
    public static class GlobalAudioExtensions
    {
        public static IScopeBuilder AddAudio(this IScopeBuilder builder)
        {
            builder.RegisterComponent(GlobalPrefabs.GlobalAudioPlayer)
                   .As<IAudioVolume>()
                   .As<IAudioPlayer>()
                   .As<IScopeSetup>();

            builder.RegisterComponent(GlobalPrefabs.GlobalAudioListener)
                   .As<IAudioListener>()
                   .As<IScopeBaseSetup>();

            return builder;
        }
    }
}