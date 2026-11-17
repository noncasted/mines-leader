using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Deploy;

[GenerateSerializer]
[GrainState(Table = "cluster", State = "deploy", Lookup = "Deploy", Key = GrainKeyType.Guid)]
public class DeployState : IDirectStateValue, IDeployScopedState
{
    [Id(0)] [JsonProperty] public Guid DeployId { get; private set; }
    [Id(1)] [JsonProperty] public DateTime LastHeartbeat { get; private set; }
    [Id(2)] [JsonProperty] public bool CoordinatorReady { get; private set; }

    // Orders the deploys: a host follows the deploy id pipe only to a deploy created after its own. A state
    // written before this field reads as MinValue, older than any new deploy.
    [Id(3)] [JsonProperty] public DateTime CreatedAt { get; private set; }

    public int Version => 0;

    public void Init(Guid deployId, DateTime now)
    {
        DeployId = deployId;
        CreatedAt = now;
        LastHeartbeat = now;
        CoordinatorReady = false;
    }

    public void Heartbeat(DateTime now)
    {
        LastHeartbeat = now;
    }

    public void MarkCoordinatorReady(DateTime now)
    {
        CoordinatorReady = true;
        LastHeartbeat = now;
    }
}