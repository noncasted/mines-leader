using Internal;

namespace GamePlay.Cards
{
    public static class CardServicesExtensions
    {
        public static IScopeBuilder AddCardServices(this IScopeBuilder builder)
        {
            builder.Register<CardFactory>();

            builder.Register<CardResourceFloatingText>()
                   .As<ICardResourceFloatingText>();

            builder.AddAllCardActionSyncs();
            builder.Register<CardActionSyncDispatcher>()
                   .As<ICardActionSyncDispatcher>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}