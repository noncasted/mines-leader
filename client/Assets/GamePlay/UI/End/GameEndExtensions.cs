using Internal;

namespace GamePlay.UI
{
    public static class GameEndExtensions
    {
        public static IScopeBuilder AddGameEndServices(this IScopeBuilder builder)
        {
            builder.Register<GameEnd>()
                   .As<IGameEnd>();

            builder.Register<RematchAwaiter>()
                   .As<IRematchAwaiter>();

            builder.RegisterCommand<RematchCommands.Failure>();
            builder.RegisterCommand<RematchCommands.Success>();

            return builder;
        }
    }
}