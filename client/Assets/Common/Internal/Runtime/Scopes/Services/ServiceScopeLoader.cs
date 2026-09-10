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
            var sceneName = options.SceneLoader.Name;

            // Этапы одинаковы для всех скоупов, поэтому замер живёт здесь, а не в каждом
            // расширении: в трассу они ложатся под тем этапом, который скоуп и открыл.
            using var stage = GameProfiler.Scope($"Scope: {sceneName}");

            var sceneLoader = new ServiceScopeSceneLoader(_sceneLoader);

            IServiceScopeBinder binder;

            using (GameProfiler.Scope("Registry scene"))
                binder = await options.SceneLoader.Load(sceneLoader);

            var builder = CreateBuilder();

            using (GameProfiler.Scope("Construct"))
                await options.ConstructCallback.Invoke(builder);

            using (GameProfiler.Scope("Assets"))
                await builder.Events.InvokeBeforeBuild();

            IContainer container;

            using (GameProfiler.Scope("Container"))
            {
                builder.RegisterInstance(builder.Events);
                container = ScopeContainer.Create(options.RootId, builder.ContainerBuilder);
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
                var lifetime = options.Parent.Lifetime.Child();
                var containerBuilder = new ContainerBuilder(sceneName, options.Parent.Container, lifetime);

                return new ScopeBuilder(
                    containerBuilder,
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