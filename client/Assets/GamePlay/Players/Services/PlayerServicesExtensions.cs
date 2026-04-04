using Internal;

namespace GamePlay.Players
{
    public static class PlayerServicesExtensions
    {
        public static IScopeBuilder AddPlayerServices(this IScopeBuilder builder)
        {
            builder.Register<GamePlayerFactory>()
                .As<IScopeSetup>();

            return builder;
        }
    }
}