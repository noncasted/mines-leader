using Internal;

namespace Global.Audio
{
    public static class GlobalAudioExtensions
    {
        public static IScopeBuilder AddAudio(this IScopeBuilder builder)
        {
            builder.RegisterComponent(Prefabs.Global.GlobalAudioPlayer)
                   .As<IAudioVolume>()
                   .As<IAudioPlayer>()
                   .As<IScopeSetup>();

            builder.RegisterComponent(Prefabs.Global.GlobalAudioListener)
                   .As<IAudioListener>()
                   .As<IScopeBaseSetup>();

            return builder;
        }
    }
}
