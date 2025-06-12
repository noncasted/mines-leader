using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Internal
{
    public class InternalScopeLoader
    {
        public InternalScopeLoader(IInternalScopeConfig config)
        {
            _config = config;
        }

        private readonly IInternalScopeConfig _config;

        public ILoadedScope Load()
        {
            var profiler = new ProfilingScope("InternalScopeLoader");
            var container = Object.Instantiate(_config.Scope);
            container.name = "Internal_Scope";
            
            Object.DontDestroyOnLoad(container);

            using (LifetimeScope.Enqueue(Register))
                container.Build();

            profiler.Dispose();

            var result = new InternalLoadedScope(container, new Lifetime());
            container.AttachScope(result);

            return result;

            void Register(IContainerBuilder containerBuilder)
            {
                var optionsRegistry = _config.AssetsStorage.Options[_config.Platform];
                optionsRegistry.CacheRegistry();
                optionsRegistry.AddOptions(new PlatformOptions(_config.Platform, Application.isMobilePlatform));
                
                _config.AssetsStorage.Cache();
                
                var assets = new AssetEnvironment(_config.AssetsStorage, optionsRegistry);
                var scopeBuilder = new InternalScopeBuilder(assets, containerBuilder);

                scopeBuilder
                    .AddScenes()
                    .AddScopeLoaders();

                containerBuilder.RegisterInstance(assets)
                    .As<IAssetEnvironment>();
            }
        }
    }
}