using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public static class InternalScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadInternal(this IServiceScopeLoader loader)
        {
            var options = new ScopeLoadOptions(null, Construct);

            using var stage = GameProfiler.Scope("Startup");

            var scope = await loader.Load(options);
            await scope.Initialize();

            Application.quitting += () => {
                Debug.Log("Internal scope lifetime terminated, disposing loaded scope.");
                scope.Dispose().Forget();
            };

            return scope;
        }

        // Корень всей цепочки скоупов. Лежит в Internal, чтобы Global мог объявить его родителем.
        public static async UniTask Construct(IScopeBuilder builder)
        {
            // Опции читаются прямо при регистрации, поэтому группа грузится до неё.
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
