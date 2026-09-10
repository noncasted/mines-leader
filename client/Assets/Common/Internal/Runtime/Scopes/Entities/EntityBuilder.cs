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
            ScopeLifetime = scopeLifetime;
            Events = events;
            View = view;
        }

        public IServiceCollection Services { get; }
        public ServiceCollection InternalServices { get; }
        public ILifetime ScopeLifetime { get; }
        public IEventLoop Events { get; }
        public IReadOnlyLifetime Lifetime => ScopeLifetime;
        public IScopeEntityView View { get; }
    }

    public static class EntityBuilderExtensions
    {
        public static T Get<T>(this IEntityScopeResult result)
        {
            return result.Container.Resolve<T>();
        }
    }
}
