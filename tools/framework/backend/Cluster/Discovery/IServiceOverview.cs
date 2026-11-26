namespace Cluster.Discovery;

public interface IServiceOverview
{
    Guid Id { get; }
    ServiceTag Tag { get; }
    DateTime UpdateTime { get; }
}
