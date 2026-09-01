namespace Internal
{
    public class ScopeBuilder : IScopeBuilder
    {
        public ScopeBuilder(
            ServiceCollection services,
            ISceneLoader sceneLoader,
            IServiceScopeBinder binder,
            ILifetime scopeLifetime,
            ILoadedScope parent,
            IEventLoop events,
            bool isMock)
        {
            Services = services;
            ServicesInternal = services;
            SceneLoader = sceneLoader;
            Binder = binder;
            ScopeLifetime = scopeLifetime;
            IsMock = isMock;
            Events = events;
            Parent = parent;
        }

        public IServiceCollection Services { get; }
        public ISceneLoader SceneLoader { get; }
        public IServiceScopeBinder Binder { get; }
        public IEventLoop Events { get; }
        public IReadOnlyLifetime Lifetime => ScopeLifetime;
        public ILoadedScope Parent { get; }
        public ILifetime ScopeLifetime { get; }
        public bool IsMock { get; }

        public ServiceCollection ServicesInternal { get; }
    }
}