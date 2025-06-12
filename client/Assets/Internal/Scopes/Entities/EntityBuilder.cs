using VContainer;
using VContainer.Unity;

namespace Internal
{
    public class EntityBuilder : IEntityBuilder
    {
        public EntityBuilder(
            ServiceCollection services,
            IScopeEntityView view,
            ILifetime scopeLifetime,
            IAssetEnvironment assets,
            IScopeEventListeners events)
        {
            Services = services;
            InternalServices = services;
            Scope = view.Scope;
            ScopeLifetime = scopeLifetime;
            Assets = assets;
            Events = events;
            View = view;
        }

        public IServiceCollection Services { get; }
        public ServiceCollection InternalServices { get; }
        public LifetimeScope Scope { get; }
        public ILifetime ScopeLifetime { get; }
        public IAssetEnvironment Assets { get; }
        public IScopeEventListeners Events { get; }
        public IReadOnlyLifetime Lifetime => ScopeLifetime;

        public IScopeEntityView View { get; }
    }
    
    public static class EntityBuilderExtensions
    {
        public static T Get<T>(this IEntityScopeResult result)
        {
            return result.Scope.Container.Resolve<T>();
        }
    }
}