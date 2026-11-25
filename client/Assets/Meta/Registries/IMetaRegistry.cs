namespace Meta
{
    /// <summary>
    /// A registry is not built in the constructor: its sprites download in parallel with the startup, and services
    /// that depend on the registry are created earlier. MetaLoop builds them after the preload.
    /// </summary>
    public interface IMetaRegistry
    {
        void Initialize();
    }
}
