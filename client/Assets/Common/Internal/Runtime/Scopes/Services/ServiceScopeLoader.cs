using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IServiceScopeLoader
    {
        UniTask<ILoadedScope> Load(ScopeLoadOptions options);
    }

    public class ServiceScopeLoader : IServiceScopeLoader
    {
        public async UniTask<ILoadedScope> Load(ScopeLoadOptions options)
        {
            var sceneName = options.SceneLoader.Name;

            // Этапы одинаковы для всех скоупов, поэтому замер живёт здесь, а не в каждом
            // расширении: в трассу они ложатся под тем этапом, который скоуп и открыл.
            using var stage = GameProfiler.Scope($"Scope: {sceneName}");

            var sceneLoader = new ServiceScopeSceneLoader();

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

            var loadResult = new LoadedScope(
                container,
                builder.ScopeLifetime,
                builder.Events,
                sceneLoader.Results);

            return loadResult;

            // Без родителя строится корень: его контейнер ни на кого не опирается.
            ScopeBuilder CreateBuilder()
            {
                if (options.Parent == null)
                {
                    var rootLifetime = new Lifetime();

                    return new ScopeBuilder(
                        new ContainerBuilder(sceneName, rootLifetime),
                        sceneLoader,
                        binder,
                        rootLifetime,
                        null,
                        new EventLoop(),
                        options.IsMock);
                }

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