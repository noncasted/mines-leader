using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Monitoring;

[GenerateSerializer]
public class ConnectedUserEntry
{
    [Id(0)] public Guid UserId { get; init; }
}
