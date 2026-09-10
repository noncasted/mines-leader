using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IServiceScopeLoader
    {
        UniTask<ILoadedScope> Load(ScopeLoadOptions options);
    }

    public class ServiceScopeLoader : IServiceScopeLoader
    {
        public ServiceScopeLoader(ISceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        private readonly ISceneLoader _sceneLoader;

        public async UniTask<ILoadedScope> Load(ScopeLoadOptions options)
        {
            var sceneName = options.ServiceSceneName ?? "Services";

            // Этапы одинаковы для всех скоупов, поэтому замер живёт здесь, а не в каждом
            // расширении: в трассу они ложатся под тем этапом, который скоуп и открыл.
            using var stage = GameProfiler.Scope($"Scope: {sceneName}");

            var sceneLoader = new ServiceScopeSceneLoader(_sceneLoader);

            ILoadedScene servicesScene;

            // Сцена сервисов нужна только как контейнер для объектов скоупа. Если ассета
            // нет, она создаётся на ходу: пустая сцена в бандле стоит открытия файла и
            // пары кадров на async-загрузке, а полезной нагрузки в ней ноль.
            using (GameProfiler.Scope("Services scene"))
            {
                servicesScene = options.ServiceScene == null
                    ? sceneLoader.Create(sceneName)
                    : await sceneLoader.Load(options.ServiceScene);
            }

            var builder = CreateBuilder();

            using (GameProfiler.Scope("Construct"))
                await options.ConstructCallback.Invoke(builder);

            using (GameProfiler.Scope("Assets"))
                await builder.Events.InvokeBeforeBuild();

            IContainer container;

            using (GameProfiler.Scope("Container"))
            {
                builder.RegisterInstance(builder.Events);
                container = ScopeContainer.Create(options.RootId, builder.ServicesInternal.Builder);
            }

            builder.Events.Bind(container);

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
                var containerBuilder = new ContainerBuilder(sceneName, options.Parent.Container, lifetime);
                var services = new ServiceCollection(containerBuilder);

                return new ScopeBuilder(
                    services,
                    sceneLoader,
                    binder,
                    lifetime,
                    options.Parent,
                    new EventLoop(),
                    options.IsMock);
            }
        }
    }
}
