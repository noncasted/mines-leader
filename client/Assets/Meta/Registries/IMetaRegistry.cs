namespace Meta
{
    /// <summary>
    /// Реестр собирается не в конструкторе: его спрайты качаются параллельно старту, а сервисы
    /// с реестром в зависимостях создаются раньше. Собирает их MetaLoop после предзагрузки.
    /// </summary>
    public interface IMetaRegistry
    {
        void Initialize();
    }
}
