using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Monitoring;

[GenerateSerializer]
public class LiveSessionEntry
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public List<Guid> UserIds { get; init; } = [];
    [Id(2)] public DateTime CreatedAt { get; init; }
}
