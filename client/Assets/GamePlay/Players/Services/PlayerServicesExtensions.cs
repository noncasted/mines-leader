using Internal;

namespace GamePlay.Players
{
    public static class PlayerServicesExtensions
    {
        public static IScopeBuilder AddPlayerServices(this IScopeBuilder builder)
        {
            builder.Register<GamePlayerFactory>()
                .WithAsset<GamePlayerFactoryOptions>()
                .As<IGamePlayerFactory>()
                .As<IScopeSetup>();
            
            return builder;
        }
    }
}