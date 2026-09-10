namespace Internal
{
    public interface IEntityBuilder : IBuilder
    {
        ILifetime ScopeLifetime { get; }
        IScopeEntityView View { get; }
    }

    public class EntityBuilder : IEntityBuilder
    {
        public EntityBuilder(
            ContainerBuilder containerBuilder,
            IScopeEntityView view,
            ILifetime scopeLifetime,
            IEventLoop events)
        {
            ContainerBuilder = containerBuilder;
            ScopeLifetime = scopeLifetime;
            Events = events;
            View = view;

            containerBuilder.AttachBuilder(this);
        }


        public ContainerBuilder ContainerBuilder { get; }
        public ILifetime ScopeLifetime { get; }
        public IContainerRegistry Registry => ContainerBuilder;
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