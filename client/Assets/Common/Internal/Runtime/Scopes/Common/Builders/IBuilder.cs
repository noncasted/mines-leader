namespace Internal
{
    public interface IBuilder
    {
        IServiceCollection Services { get; }
        IEventLoop Events { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}