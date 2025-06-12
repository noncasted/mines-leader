using Internal;

namespace GamePlay.Cards
{
    public static class CardRootExtensions 
    {
        public static IEntityBuilder AddCardLocalRoot(this IEntityBuilder builder)
        {
            builder.Register<LocalCard>()
                .WithParameter(builder.ScopeLifetime)
                .As<ILocalCard>()
                .As<ICard>();

            return builder;
        }
        
        public static IEntityBuilder AddCardRemoteRoot(this IEntityBuilder builder)
        {
            builder.Register<RemoteCard>()
                .WithParameter(builder.ScopeLifetime)
                .As<IRemoteCard>()
                .As<ICard>()
                .As<IScopeSetup>();

            return builder;
        }
    }
}