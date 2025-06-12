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
                .AsBackendProjection<BackendUserContexts.ProfileProjection>()
                .As<IBackendUser>();

            builder.Register<BackendMatchmaking>()
                .AsBackendProjection<MatchmakingContexts.GameResult>()
                .AsBackendProjection<MatchmakingContexts.LobbyResult>()
                .WithAsset<BackendOptions>()
                .As<IBackendMatchmaking>();

            return builder;
        }
    }
}