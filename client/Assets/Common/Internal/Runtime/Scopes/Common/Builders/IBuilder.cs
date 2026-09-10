namespace Internal
{
    public interface IBuilder
    {
        IContainerRegistry Registry { get; }
        IEventLoop Events { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}