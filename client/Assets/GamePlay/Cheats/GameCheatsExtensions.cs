using Internal;

namespace GamePlay.Cheats
{
    public static class GameCheatsExtensions
    {
        // The cheats of the editor window; a release build has neither the service nor its classes.
        public static IScopeBuilder AddGameCheats(this IScopeBuilder builder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<GameCheats>()
                   .As<IScopeSetup>();
#endif
            return builder;
        }
    }
}
