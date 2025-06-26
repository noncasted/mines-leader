using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Internal
{
    public class ServiceScopeLoader : IServiceScopeLoader
    {
        public ServiceScopeLoader(
            IAssetEnvironment assets,
            ISceneLoader sceneLoader)
        {
            _assets = assets;
            _sceneLoader = sceneLoader;
        }

        private readonly IAssetEnvironment _assets;
        private readonly ISceneLoader _sceneLoader;

        public IAssetEnvironment Assets => _assets;

        public async UniTask<ILoadedScope> Load(ScopeLoadOptions options)
        {
            var sceneLoader = new ServiceScopeSceneLoader(_sceneLoader);
            var servicesScene = await sceneLoader.Load(options.ServiceScene);

            var builder = CreateBuilder();

            var containerObject = new GameObject("ScopeLifetime");
            var container = containerObject.AddComponent<LifetimeScope>();
            builder.Binder.MoveToModules(container);
            
            await options.ConstructCallback.Invoke(builder);
            
            BuildContainer();

            var eventLoop = container.Container.Resolve<IEventLoop>();
            await eventLoop.RunConstruct(builder.ScopeLifetime);

            var loadResult = new ScopeLoadResult(
                container,
                builder.ScopeLifetime,
                eventLoop,
                sceneLoader.Results);

            return loadResult;

            ScopeBuilder CreateBuilder()
            {
                var binder = new ServiceScopeBinder(servicesScene.Instance);
                var lifetime = options.Parent.Lifetime.Child();
                var services = new ServiceCollection();
                
                return new ScopeBuilder(
                    services,
                    _assets,
                    sceneLoader,
                    binder,
                    lifetime,
                    options.Parent,
                    new ScopeEventListeners(),
                    options.IsMock);
            }
            
            void BuildContainer()
            {
                using (LifetimeScope.EnqueueParent(options.Parent.Container))
                {
                    using (LifetimeScope.Enqueue(Register))
                    {
                        container.Build();
                    }
                }

                builder.ServicesInternal.Resolve(container.Container);
                return;

                void Register(IContainerBuilder containerBuilder)
                {
                    builder.AddEvents();
                    builder.Register<IViewInjector, ViewInjector>(VContainer.Lifetime.Scoped);
                    builder.Events.Register(containerBuilder);
                    builder.ServicesInternal.PassRegistrations(containerBuilder);
                }
            }
        }
    }
}