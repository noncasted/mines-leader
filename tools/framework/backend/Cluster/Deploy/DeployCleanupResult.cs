namespace Cluster.Deploy;

public class DeployCleanupResult
{
    public required int Removed { get; init; }
    public required IReadOnlyList<Guid> RemovedDeployIds { get; init; }
    public required Guid KeptDeployId { get; init; }
}
