using Internal;

namespace GamePlay.Cards
{
    public static class CardComponentsExtensions
    {
        public static IEntityBuilder AddCardLocalComponents(this IEntityBuilder builder)
        {
            builder.Register<CardDropDetector>()
                .As<ICardDropDetector>();
            
            builder.Register<CardStateContext>()
                .WithParameter(builder.Lifetime)
                .As<ICardStateContext>();

            builder.Register<CardDropArea>()
                .As<ICardDropArea>();

            builder.Register<CardActionState>()
                .As<ICardActionState>()
                .As<IScopeSetup>();

            return builder;
        }
        
        public static IEntityBuilder AddCardRemoteComponents(this IEntityBuilder builder)
        {
            builder.Register<CardDropArea>()
                .As<ICardDropArea>();

            builder.Register<CardStateContext>()
                .WithParameter(builder.Lifetime)
                .As<ICardStateContext>();

            return builder;
        }
    }
}