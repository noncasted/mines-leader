namespace Internal
{
    public class ScopeBuilder : IScopeBuilder
    {
        public ScopeBuilder(
            ServiceCollection services,
            IAssetEnvironment assets,
            ISceneLoader sceneLoader,
            IServiceScopeBinder binder,
            ILifetime scopeLifetime,
            ILoadedScope parent,
            IScopeEventListeners events,
            bool isMock)
        {
            Services = services;
            ServicesInternal = services;
            Assets = assets;
            SceneLoader = sceneLoader;
            Binder = binder;
            ScopeLifetime = scopeLifetime;
            IsMock = isMock;
            Events = events;
            Parent = parent;
        }
        public IServiceCollection Services { get; }
        public IAssetEnvironment Assets { get; }
        public ISceneLoader SceneLoader { get; }
        public IServiceScopeBinder Binder { get; }
        public IScopeEventListeners Events { get; }
        public IReadOnlyLifetime Lifetime => ScopeLifetime;
        public ILoadedScope Parent { get; }
        public ILifetime ScopeLifetime { get; }
        public bool IsMock { get; }
        
        public ServiceCollection ServicesInternal { get; }
    }
}