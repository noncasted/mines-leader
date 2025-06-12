using Internal;
using Shared;

namespace Global.Backend
{
    public static class GlobalBackendExtensions
    {
        public static IScopeBuilder AddBackend(this IScopeBuilder builder)
        {
            builder.Register<BackendGetGateway>()
                .As<IBackendGetGateway>();

            builder.Register<BackendMediaGateway>()
                .As<IBackendMediaGateway>();

            builder.Register<BackendPostGateway>()
                .As<IBackendPostGateway>();

            builder.Register<BackendClient>()
                .WithAsset<BackendOptions>()
                .As<IBackendClient>();

            builder.Register<BackendService>()
                .WithAsset<BackendOptions>()
                .As<IBackend>();

            builder.Register<BackendProjectionHub>()
                .WithAsset<BackendOptions>()
                .As<IBackendProjectionHub>();

            builder.Register<BackendUser>()
                .As<IScopeSetup>()
                .As<IBackendUser>();

            builder.Register<BackendMatchmaking>()
                .WithAsset<BackendOptions>()
                .As<IScopeSetup>()
                .As<IBackendMatchmaking>();

            builder
                .RegisterBackendProjection<BackendUserContexts.ProfileProjection>()
                .RegisterBackendProjection<BackendUserContexts.DeckProjection>()
                .RegisterBackendProjection<MatchmakingContexts.GameResult>()
                .RegisterBackendProjection<MatchmakingContexts.LobbyResult>();

            return builder;
        }
    }
}