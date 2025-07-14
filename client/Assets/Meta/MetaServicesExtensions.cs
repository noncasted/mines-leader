using Global.Backend;
using Internal;
using Shared;

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
                .WithAsset<BackendOptions>()
                .WithScopeLifetime()
                .As<IMetaBackend>();
            
            builder.Register<Matchmaking>()
                .As<IMatchmaking>();

            builder.Register<User>()
                .As<IUser>();

            builder.RegisterAsset<CharacterAvatars>();

            builder.AddNetworkConnection();

            builder.RegisterCommand<BackendProjectionHub>();

            builder
                .RegisterBackendProjection<BackendUserContexts.ProfileProjection>()
                .RegisterBackendProjection<BackendUserContexts.DeckProjection>()
                .RegisterBackendProjection<MatchmakingContexts.GameResult>()
                .RegisterBackendProjection<MatchmakingContexts.LobbyResult>();
            
            return builder;
        }
    }
}