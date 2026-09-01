using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Lifetime = Internal.Lifetime;

namespace Flow.Startup
{
    public class InternalScopeLoader
    {
        public async UniTask<ILoadedScope> Load()
        {
            using var stage = GameProfiler.Scope("Startup");

            // Каталог ассетов лежит в Resources целиком, поэтому грузится один раз здесь,
            // до первого обращения к любой группе.
            using (GameProfiler.Scope("Assets catalog"))
                AssetCatalog.Load();

            // The whole Global prefab group lives for the application lifetime, so it is retained once here.
            using (GameProfiler.Scope("Prefabs: Global"))
                await GlobalPrefabs.Group.Retain();

            LifetimeScope scopeObject;

            using (GameProfiler.Scope("Scope object"))
            {
                var containerObject = new GameObject("Startup (ScopeLifetime)");
                scopeObject = containerObject.AddComponent<LifetimeScope>();

                Object.DontDestroyOnLoad(scopeObject);
            }

            using (GameProfiler.Scope("Container"))
            {
                using (LifetimeScope.Enqueue(Register))
                    scopeObject.Build();
            }

            var result = new InternalLoadedScope(scopeObject, new Lifetime());
            scopeObject.GetObjectLifetime().Listen(() => {
                Debug.Log($"Internal scope lifetime terminated, disposing loaded scope.");
                result.Dispose();
            });

            return result;

            void Register(IContainerBuilder container)
            {
                container.Register<SceneLoader>(VContainer.Lifetime.Singleton)
                                     .As<ISceneLoader>();
                
                container.Register<ServiceScopeLoader>(VContainer.Lifetime.Singleton)
                                     .As<IServiceScopeLoader>();

                container.Register<EntityScopeLoader>(VContainer.Lifetime.Singleton)
                                     .As<IEntityScopeLoader>();

                InternalAssets.OptionsContainer.Register(container);
            }
        }
    }
}