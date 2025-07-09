using Internal;

namespace GamePlay.Cards
{
    public static class CardComponentsExtensions
    {
        public static IEntityBuilder AddCardLocalComponents(this IEntityBuilder builder)
        {
            builder.Register<CardDropDetector>()
                .As<ICardDropDetector>();

            builder.Register<CardStateLifetime>()
                .WithParameter(builder.Lifetime)
                .As<ICardStateLifetime>();

            builder.Register<CardDropArea>()
                .As<ICardDropArea>();

            builder.Register<CardContext>()
                .As<ICardContext>()
                .As<IScopeSetup>();

            builder.Register<CardUseSyncSender>()
                .As<ICardUseSync>();

            return builder;
        }

        public static IEntityBuilder AddCardRemoteComponents(this IEntityBuilder builder)
        {
            builder.Register<CardDropArea>()
                .As<ICardDropArea>();

            builder.Register<CardStateLifetime>()
                .WithParameter(builder.Lifetime)
                .As<ICardStateLifetime>();

            builder.Register<CardUseSyncReceiver>()
                .As<IScopeSetup>();

            return builder;
        }
    }
}