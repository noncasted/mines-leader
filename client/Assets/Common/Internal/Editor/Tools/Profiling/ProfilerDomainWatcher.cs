using UnityEditor;

namespace Internal
{
    /// <summary>
    /// Отмечает первый запуск после перекомпиляции. Домен при входе в плей-мод не
    /// перезагружается, поэтому статический конструктор здесь срабатывает ровно тогда,
    /// когда сборки пересобрались, а редактор сбросил кеш ассетов — такой запуск
    /// холодный и его цифры с тёплым сравнивать нельзя.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProfilerDomainWatcher
    {
        static ProfilerDomainWatcher()
        {
            ProfilerTraceStorage.ColdDomain = true;
        }
    }
}
