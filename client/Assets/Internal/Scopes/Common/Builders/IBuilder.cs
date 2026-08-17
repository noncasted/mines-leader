namespace Internal
{
    public interface IBuilder
    {
        IServiceCollection Services { get; }
        IAssetEnvironment Assets { get; }
        IEventLoop Events { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}