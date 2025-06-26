using Internal;
using Shared;

namespace Global.Backend
{
    public static class GlobalBackendExtensions
    {
        public static IScopeBuilder AddBackend(this IScopeBuilder builder)
        {
            builder.Register<BackendGet>()
                .As<IBackendGet>();

            builder.Register<BackendMedia>()
                .As<IBackendMedia>();

            builder.Register<BackendPost>()
                .As<IBackendPost>();

            builder.Register<BackendClient>()
                .WithAsset<BackendOptions>()
                .As<IBackendClient>();

            builder.Register<BackendProjectionHub>()
                .WithAsset<BackendOptions>()
                .As<IBackendProjectionHub>();

            builder
                .RegisterBackendProjection<BackendUserContexts.ProfileProjection>()
                .RegisterBackendProjection<BackendUserContexts.DeckProjection>()
                .RegisterBackendProjection<MatchmakingContexts.GameResult>()
                .RegisterBackendProjection<MatchmakingContexts.LobbyResult>();

            return builder;
        }
    }
}