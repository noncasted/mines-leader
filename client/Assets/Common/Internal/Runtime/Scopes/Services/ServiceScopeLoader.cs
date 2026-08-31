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
            // Этапы одинаковы для всех скоупов, поэтому замер живёт здесь, а не в каждом
            // расширении: в трассу они ложатся под тем этапом, который скоуп и открыл.
            using var stage = GameProfiler.Scope($"Scope: {options.ServiceSceneName ?? "Services"}");

            var sceneLoader = new ServiceScopeSceneLoader(_sceneLoader);

            ILoadedScene servicesScene;

            // Сцена сервисов нужна только как контейнер для объектов скоупа. Если ассета
            // нет, она создаётся на ходу: пустая сцена в бандле стоит открытия файла и
            // пары кадров на async-загрузке, а полезной нагрузки в ней ноль.
            using (GameProfiler.Scope("Services scene"))
            {
                servicesScene = options.ServiceScene == null
                    ? sceneLoader.Create(options.ServiceSceneName)
                    : await sceneLoader.Load(options.ServiceScene);
            }

            var builder = CreateBuilder();

            var containerObject = new GameObject("ScopeLifetime");
            var container = containerObject.AddComponent<LifetimeScope>();
            builder.Binder.MoveToModules(container);

            using (GameProfiler.Scope("Construct"))
                await options.ConstructCallback.Invoke(builder);

            using (GameProfiler.Scope("Assets"))
                await builder.Events.InvokeBeforeBuild();

            using (GameProfiler.Scope("Container"))
                BuildContainer();

            builder.Events.Bind(container.Container);

            using (GameProfiler.Scope("Setup"))
                await builder.Events.RunConstruct(builder.ScopeLifetime);

            var loadResult = new ScopeLoadResult(
                container,
                builder.ScopeLifetime,
                builder.Events,
                sceneLoader.Results);

            return loadResult;

            ScopeBuilder CreateBuilder()
            {
                var binder = new ServiceScopeBinder(servicesScene.Scene);
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
                    builder.RegisterInstance(builder.Events);
                    builder.Register<IViewInjector, ViewInjector>(VContainer.Lifetime.Scoped);
                    builder.ServicesInternal.PassRegistrations(containerBuilder);
                }
            }
        }
    }
}