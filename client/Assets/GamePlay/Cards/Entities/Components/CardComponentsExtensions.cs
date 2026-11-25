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

            builder.AddCardViews();
            builder.AddCardLocalViews();

            return builder;
        }

        public static IEntityBuilder AddCardRemoteComponents(this IEntityBuilder builder)
        {
            builder.Register<CardStateLifetime>()
                   .WithParameter(builder.Lifetime)
                   .As<ICardStateLifetime>();

            builder.AddCardViews();
            builder.AddCardRemoteViews();

            return builder;
        }

        // Card views are plain classes over GameCardBindings: they are not on the prefab, and both cards
        // build the same shared part.
        public static IEntityBuilder AddCardViews(this IEntityBuilder builder)
        {
            builder.Register<CardTransform>()
                   .As<ICardTransform>();

            builder.Register<CardRenderer>()
                   .As<ICardRenderer>();

            builder.Register<CardView>()
                   .As<ICardView>();

            return builder;
        }

        public static IEntityBuilder AddCardLocalViews(this IEntityBuilder builder)
        {
            builder.Register<CardPointerHandler>()
                   .As<ICardPointerHandler>()
                   .As<IScopeSetup>();

            builder.Register<CardDataView>()
                   .As<IScopeSetup>();

            builder.Register<CardAvailabilityView>()
                   .As<IScopeSetup>();

            builder.Register<CardSelectionSwitcher>()
                   .As<IScopeSetup>();

            return builder;
        }

        public static IEntityBuilder AddCardRemoteViews(this IEntityBuilder builder)
        {
            builder.Register<CardRevealView>()
                   .As<ICardRevealView>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
