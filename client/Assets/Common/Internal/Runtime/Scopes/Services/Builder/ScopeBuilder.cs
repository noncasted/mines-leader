namespace Internal
{
    public interface IScopeBuilder : IBuilder
    {
        ISceneLoader SceneLoader { get; }
        IServiceScopeBinder Binder { get; }
        ILifetime ScopeLifetime { get; }
        bool IsMock { get; }
    }

    public class ScopeBuilder : IScopeBuilder
    {
        public ScopeBuilder(
            ContainerBuilder containerBuilder,
            ISceneLoader sceneLoader,
            IServiceScopeBinder binder,
            ILifetime scopeLifetime,
            ILoadedScope parent,
            IEventLoop events,
            bool isMock)
        {
            ContainerBuilder = containerBuilder;
            SceneLoader = sceneLoader;
            Binder = binder;
            ScopeLifetime = scopeLifetime;
            IsMock = isMock;
            Events = events;
            Parent = parent;

            containerBuilder.AttachBuilder(this);
        }

        public ContainerBuilder ContainerBuilder { get; }
        public ISceneLoader SceneLoader { get; }
        public IServiceScopeBinder Binder { get; }
        public IContainerRegistry Registry => ContainerBuilder;
        public IEventLoop Events { get; }
        public IReadOnlyLifetime Lifetime => ScopeLifetime;
        public ILoadedScope Parent { get; }
        public ILifetime ScopeLifetime { get; }
        public bool IsMock { get; }
    }
}