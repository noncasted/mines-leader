using Internal;

namespace GamePlay.UI
{
    public static class GamePauseServicesExtensions
    {
        public static IScopeBuilder AddGamePauseServices(this IScopeBuilder builder)
        {
            builder.Register<GamePause>()
                   .As<IGamePause>()
                   .As<IScopeSetup>();

            builder.Register<GamePauseSettings>()
                   .As<IGamePauseSettings>();

            builder.Register<GamePauseLeave>()
                   .As<IGamePauseLeave>();

            return builder;
        }
    }
}
