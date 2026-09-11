using System.Collections.Generic;
using Internal;

namespace Meta
{
    public interface IMetaState
    {
        /// <summary>Сервер принял юзера, и его id сверен с сохранённым.</summary>
        IViewableProperty<bool> IsAuthorized { get; }

        /// <summary>Все проекции, которые сервер шлёт при подключении, приехали.</summary>
        IViewableProperty<bool> IsProjectionsReceived { get; }

        /// <summary>Реестры меты собраны.</summary>
        IViewableProperty<bool> IsRegistriesLoaded { get; }

        /// <summary>Всё сразу: по этому флагу меню достраивается и снимает загрузочный экран.</summary>
        IViewableProperty<bool> IsReady { get; }

        void SetAuthorized();
        void SetRegistriesLoaded();
    }

    /// <summary>
    /// Мета грузится параллельными ветками (подключение, реестры), и их готовность собирается здесь.
    /// Проекции отслеживаются по их собственным флагам. Ответы на запросы (матч, лобби) сюда
    /// не входят: при подключении они не приезжают.
    ///
    /// IsReady ставится после того, как отработали подписчики частного флага: сервисы, которые
    /// достраиваются по нему (например, колоды по реестрам), успевают до открытия меню.
    /// </summary>
    public class MetaState : IMetaState, IScopeSetup
    {
        public MetaState(IReadOnlyList<IInitialBackendProjection> projections)
        {
            _projections = projections;
        }

        private readonly IReadOnlyList<IInitialBackendProjection> _projections;

        private readonly ViewableProperty<bool> _isAuthorized = new(false);
        private readonly ViewableProperty<bool> _isProjectionsReceived = new(false);
        private readonly ViewableProperty<bool> _isRegistriesLoaded = new(false);
        private readonly ViewableProperty<bool> _isReady = new(false);

        public IViewableProperty<bool> IsAuthorized => _isAuthorized;
        public IViewableProperty<bool> IsProjectionsReceived => _isProjectionsReceived;
        public IViewableProperty<bool> IsRegistriesLoaded => _isRegistriesLoaded;
        public IViewableProperty<bool> IsReady => _isReady;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            foreach (var projection in _projections)
                projection.IsInitialized.AdviseTrue(lifetime, CheckProjections);

            // Часть проекций могла приехать ещё до сетапа.
            CheckProjections();
        }

        public void SetAuthorized()
        {
            _isAuthorized.Set(true);
            CheckReady();
        }

        public void SetRegistriesLoaded()
        {
            _isRegistriesLoaded.Set(true);
            CheckReady();
        }

        private void CheckProjections()
        {
            if (_isProjectionsReceived.Value == true)
                return;

            foreach (var projection in _projections)
            {
                if (projection.IsInitialized.Value == false)
                    return;
            }

            _isProjectionsReceived.Set(true);
            CheckReady();
        }

        private void CheckReady()
        {
            if (_isAuthorized.Value == false)
                return;

            if (_isProjectionsReceived.Value == false)
                return;

            if (_isRegistriesLoaded.Value == false)
                return;

            _isReady.Set(true);
        }
    }
}
