using System.Collections.Generic;
using Internal;

namespace Meta
{
    public interface IMetaState
    {
        /// <summary>The server accepted the user and its id matches the saved one.</summary>
        IViewableProperty<bool> IsAuthorized { get; }

        /// <summary>All projections the server sends on connect have arrived.</summary>
        IViewableProperty<bool> IsProjectionsReceived { get; }

        /// <summary>The meta registries are built.</summary>
        IViewableProperty<bool> IsRegistriesLoaded { get; }

        /// <summary>Everything at once: by this flag the menu finishes building and removes the loading screen.</summary>
        IViewableProperty<bool> IsReady { get; }

        void SetAuthorized();
        void SetRegistriesLoaded();
    }

    /// <summary>
    /// The meta loads in parallel branches (connection, registries), and their readiness is gathered here.
    /// Projections are tracked by their own flags. Replies to requests (match, lobby) are not
    /// included: they do not arrive on connect.
    ///
    /// IsReady is set after the subscribers of the private flag have run: services that
    /// finish building by it (for example, decks by registries) are done before the menu opens.
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

            // Some projections may have arrived before the setup.
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
