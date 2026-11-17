using Infrastructure;

namespace Cluster.Deploy;

[GenerateSerializer]
public class DeployIdResponse
{
    [Id(0)] public Guid DeployId { get; init; }
}
