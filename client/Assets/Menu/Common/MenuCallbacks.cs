using Internal;

namespace Menu.Common
{
    /// <summary>
    /// Мета готова: авторизация прошла и все проекции подключения приехали. Меню грузится
    /// параллельно с метой, поэтому всё, что читает её данные, строится здесь, а не в OnSetup.
    /// Экран загрузки снимается сразу после этого события (см. MenuLoop).
    /// </summary>
    public interface IMetaSetupCompleted
    {
        void OnMetaSetupCompleted(IReadOnlyLifetime lifetime);
    }
}
