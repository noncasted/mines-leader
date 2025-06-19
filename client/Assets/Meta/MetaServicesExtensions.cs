using Global.GameLoops;
using Internal;

namespace Meta
{
    public static class MetaServicesExtensions
    {
        public static IScopeBuilder AddMetaServices(this IScopeBuilder builder)
        {
            builder.Register<MetaLoop>()
                .As<IScopeBaseSetupAsync>();
            
            builder.Register<Authentication>()
                .As<IAuthentication>();

            builder.RegisterScriptableRegistry<CardsRegistry, CardDefinition>()
                .As<ICardsRegistry>();

            builder.Register<DeckService>()
                .WithScopeLifetime()
                .As<IDeckService>()
                .As<IScopeSetup>();

            builder.Register<MetaBackend>()
                .WithScopeLifetime()
                .As<IMetaBackend>();
            
            builder.Register<Matchmaking>()
                .As<IMatchmaking>();

            builder.Register<User>()
                .As<IUser>();

            builder.RegisterAsset<CharacterAvatars>();
            builder.AddFromFactory<BaseGameLoopFactory>();
            
            return builder;
        }
    }
}