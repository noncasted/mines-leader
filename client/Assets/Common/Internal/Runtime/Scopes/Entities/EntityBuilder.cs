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
            IEventLoop events)
        {
            Services = services;
            InternalServices = services;
            Scope = view.Scope;
            ScopeLifetime = scopeLifetime;
            Events = events;
            View = view;
        }

        public IServiceCollection Services { get; }
        public ServiceCollection InternalServices { get; }
        public LifetimeScope Scope { get; }
        public ILifetime ScopeLifetime { get; }
        public IEventLoop Events { get; }
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