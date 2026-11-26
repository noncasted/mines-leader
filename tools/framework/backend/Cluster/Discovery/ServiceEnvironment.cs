namespace Cluster.Discovery;

public interface IServiceEnvironment
{
    Guid ServiceId { get; }
    ServiceTag Tag { get; }
}

public class ServiceEnvironment : IServiceEnvironment
{
    public Guid ServiceId { get; } = Guid.NewGuid();
    public required ServiceTag Tag { get; init; }
}
