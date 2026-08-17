using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Internal
{
    public interface IServiceScopeLoader
    {
        UniTask<ILoadedScope> Load(ScopeLoadOptions options);
    }
    
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

        public async UniTask<ILoadedScope> Load(ScopeLoadOptions options)
        {
            var sceneLoader = new ServiceScopeSceneLoader(_sceneLoader);
            var servicesScene = await sceneLoader.Load(options.ServiceScene);

            var builder = CreateBuilder();

            var containerObject = new GameObject("ScopeLifetime");
            var container = containerObject.AddComponent<LifetimeScope>();
            builder.Binder.MoveToModules(container);

            await options.ConstructCallback.Invoke(builder);
            await builder.Events.InvokeBeforeBuild();

            BuildContainer();

            builder.Events.Bind(container.Container);
            await builder.Events.RunConstruct(builder.ScopeLifetime);

            var loadResult = new ScopeLoadResult(
                container,
                builder.ScopeLifetime,
                builder.Events,
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
                    new EventLoop(),
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
                    builder.Register<IViewInjector, ViewInjector>(VContainer.Lifetime.Scoped);
                    builder.ServicesInternal.PassRegistrations(containerBuilder);
                }
            }
        }
    }
}