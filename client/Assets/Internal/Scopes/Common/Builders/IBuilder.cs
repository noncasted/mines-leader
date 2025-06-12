namespace Internal
{
    public interface IBuilder
    {
        IServiceCollection Services { get; }
        IAssetEnvironment Assets { get; }
        IScopeEventListeners Events { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}