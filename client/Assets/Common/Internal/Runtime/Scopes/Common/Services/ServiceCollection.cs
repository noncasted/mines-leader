namespace Internal
{
    /// <summary>
    /// Регистрации скоупа пишутся прямо в билдер контейнера: из него сгенерированный класс
    /// забирает дырки (экземпляры, компоненты, параметры) и узнаёт выбранные альтернативы.
    /// </summary>
    public class ServiceCollection : IServiceCollection
    {
        public ServiceCollection(ContainerBuilder builder)
        {
            Builder = builder;
        }

        public ContainerBuilder Builder { get; }
        public IContainerRegistry Registry => Builder;
    }
}
