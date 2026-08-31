using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using GlobalAssets = Global.Prefabs;
using Lifetime = Internal.Lifetime;

namespace Flow.Startup
{
    public class InternalScopeLoader
    {
        public InternalScopeLoader(AssetsStorage assets)
        {
            _assets = assets;
        }

        private readonly AssetsStorage _assets;

        public async UniTask<ILoadedScope> Load()
        {
            // The whole Global prefab group lives for the application lifetime, so it is retained once here.
            await GlobalAssets.Global.Retain();

            var scopeObject = Object.Instantiate(GlobalAssets.Global.InternalScope)
                                    .GetComponent<InternalScope>();
            scopeObject.name = "Internal_Scope";

            Object.DontDestroyOnLoad(scopeObject);

            using (LifetimeScope.Enqueue(Register))
                scopeObject.Build();

            var result = new InternalLoadedScope(scopeObject, new Lifetime());
            scopeObject.AttachScope(result);

            return result;

            void Register(IContainerBuilder container)
            {
                _assets.Cache();

                var assets = new AssetEnvironment(_assets);

                var preprocessors = assets.GetAssets<EnvPreprocessor>();

                foreach (var preprocessor in preprocessors)
                    preprocessor.Execute();

                container.Register<SceneLoader>(VContainer.Lifetime.Singleton)
                                     .As<ISceneLoader>();
                
                container.Register<ServiceScopeLoader>(VContainer.Lifetime.Singleton)
                                     .As<IServiceScopeLoader>();

                container.Register<EntityScopeLoader>(VContainer.Lifetime.Singleton)
                                     .As<IEntityScopeLoader>();
                
                container.RegisterInstance(assets)
                         .As<IAssetEnvironment>();

                OptionsContainer.Load().Register(container);
            }
        }
    }
}