namespace Internal
{
    /// <summary>
    /// Билдер корневого скоупа приложения: без сцены и родителя, только регистрации.
    /// </summary>
    public class RootBuilder : IBuilder
    {
        public RootBuilder(ContainerBuilder containerBuilder, IEventLoop events, IReadOnlyLifetime lifetime)
        {
            ContainerBuilder = containerBuilder;
            Events = events;
            Lifetime = lifetime;

            containerBuilder.AttachBuilder(this);
        }

        public ContainerBuilder ContainerBuilder { get; }
        public IContainerRegistry Registry => ContainerBuilder;
        public IEventLoop Events { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}
