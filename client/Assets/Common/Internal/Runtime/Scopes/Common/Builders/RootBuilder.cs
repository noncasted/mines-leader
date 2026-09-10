namespace Internal
{
    /// <summary>
    /// Билдер корневого скоупа приложения: без сцены и родителя, только регистрации.
    /// </summary>
    public class RootBuilder : IBuilder
    {
        public RootBuilder(ServiceCollection services, IEventLoop events, IReadOnlyLifetime lifetime)
        {
            Services = services;
            ServicesInternal = services;
            Events = events;
            Lifetime = lifetime;
        }

        public IServiceCollection Services { get; }
        public ServiceCollection ServicesInternal { get; }
        public IEventLoop Events { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}
