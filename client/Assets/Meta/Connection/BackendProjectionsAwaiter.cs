using System.Collections.Generic;
using Internal;

namespace Meta
{
    public interface IBackendProjectionsAwaiter
    {
        /// <summary>Все проекции, которые сервер шлёт при подключении, приехали.</summary>
        IViewableProperty<bool> IsInitialized { get; }
    }

    /// <summary>
    /// Мета не ждёт проекции в сетапе: реестры и меню грузятся параллельно с подключением.
    /// Готовность данных собирается здесь, по флагам самих проекций. Ответы на запросы
    /// (матч, лобби) сюда не входят: при подключении они не приезжают.
    /// </summary>
    public class BackendProjectionsAwaiter : IBackendProjectionsAwaiter, IScopeSetup
    {
        public BackendProjectionsAwaiter(IReadOnlyList<IInitialBackendProjection> projections)
        {
            _projections = projections;
        }

        private readonly IReadOnlyList<IInitialBackendProjection> _projections;
        private readonly ViewableProperty<bool> _isInitialized = new(false);

        public IViewableProperty<bool> IsInitialized => _isInitialized;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            foreach (var projection in _projections)
                projection.IsInitialized.AdviseTrue(lifetime, Check);

            // Часть проекций могла приехать ещё до сетапа.
            Check();
        }

        private void Check()
        {
            if (_isInitialized.Value == true)
                return;

            foreach (var projection in _projections)
            {
                if (projection.IsInitialized.Value == false)
                    return;
            }

            _isInitialized.Set(true);
        }
    }
}
