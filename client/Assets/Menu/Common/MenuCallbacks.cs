using Internal;

namespace Menu.Common
{
    /// <summary>
    /// The meta is ready: authorization passed and all connection projections arrived. The menu loads
    /// in parallel with the meta, so everything that reads its data is built here, not in OnSetup.
    /// The loading screen is removed right after this event (see MenuLoop).
    /// </summary>
    public interface IMetaSetupCompleted
    {
        void OnMetaSetupCompleted(IReadOnlyLifetime lifetime);
    }
}
