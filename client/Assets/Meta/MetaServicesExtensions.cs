using Internal;
using Network;
using Shared;

namespace Meta
{
    public static class MetaServicesExtensions
    {
        public static IScopeBuilder AddMetaServices(this IScopeBuilder builder)
        {
            builder.Register<MetaLoop>()
                   .As<IScopeBaseSetupAsync>();

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

            builder.Register<Authentication>()
                   .As<IAuthentication>();

            builder.Register<CardsRegistry>()
                   .As<ICardsRegistry>();

            builder.AddNetworkConnection();

            builder.RegisterCommand<BackendProjectionHub>();

            builder.RegisterCommand<ConnectionCompletedCommand>()
                   .As<IMetaConnectionAwaiter>();

            builder
                .RegisterBackendProjection<SharedBackendUser.ProfileProjection>()
                .RegisterBackendProjection<SharedBackendUser.DeckProjection>()
                .RegisterBackendProjection<SharedMatchmaking.MatchResult>()
                .RegisterBackendProjection<SharedMatchmaking.LobbyResult>();

            builder.Register<CardConfigs>()
                   .As<IBackendProjection<CardConfigOptions>>()
                   .As<IBackendProjection>()
                   .As<ICardConfigs>();

            return builder;
        }
    }
}