using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class InternalScopeExtensions
    {
        public static UniTask<ILoadedScope> LoadInternal(this IServiceScopeLoader loader)
        {
            return loader.LoadRoot(Construct);
        }

        // Root of the whole scope chain (the framework core loads it, the content of this game fills it). Global
        // declares it as its parent.
        public static async UniTask Construct(IScopeBuilder builder)
        {
            // Options are read right at registration, so the group is loaded before it.
            await builder.LoadEnvAssetGroup(InternalAssets.Group);

            builder.Register<ServiceScopeLoader>()
                   .As<IServiceScopeLoader>();

            builder.Register<EntityScopeLoader>()
                   .As<IEntityScopeLoader>();

            builder.Register<StartupAssetsPreload>()
                   .As<IStartupAssetsPreload>();

            InternalAssets.OptionsContainer.Register(builder);
        }
    }
}
