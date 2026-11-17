using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public static class InternalScopeExtensions
    {
        // The main content bundle of the game (the ContentBundle asset Main: the ContentRoot of the catalogues),
        // preloaded into the .data file of a WebGL build.
        private const string ContentName = "Main";

        public static UniTask<ILoadedScope> LoadInternal(this IServiceScopeLoader loader)
        {
            return loader.LoadRoot(Construct);
        }

        // Root of the whole scope chain (the framework core loads it, the content of this game fills it). Global
        // declares it as its parent.
        public static async UniTask Construct(IScopeBuilder builder)
        {
            // The bundles of the build and the catalogues bound to them, before the first load of a group or a scene.
            await builder.AddContent(new ContentOptions(ContentName));

            // Options are read right at registration, so the group is loaded before it.
            await builder.LoadEnvAssetGroup(InternalAssets.Group);

            builder.Register<ServiceScopeLoader>()
                   .As<IServiceScopeLoader>();

            builder.Register<EntityScopeLoader>()
                   .As<IEntityScopeLoader>();

            builder.Register<StartupAssetsPreload>()
                   .As<IStartupAssetsPreload>();

            InternalAssets.OptionsContainer.Register(builder);

            builder.AddNetwork(InternalAssets.NetworkOptions, ProtocolHash.Value);

            // The latency overlay and the outdated client notice stay Screen Space - Overlay canvases.
            builder.AddScreenCanvasOverlay();
        }
    }
}
