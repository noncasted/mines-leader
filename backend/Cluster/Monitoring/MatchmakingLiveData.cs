using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Shared;

namespace Cluster.Monitoring;

// Registered as a deploy-scoped state so that IDeployCleanup removes the rows of old deploys. The rows hold an
// AddressableStateValue (DeploymentState), the state name is the one DeploymentState.CreateDefaultStateIdentity gives.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_MatchmakingLiveData", Lookup = "MatchmakingLive", Key = GrainKeyType.Guid)]
public class MatchmakingLiveData : IDirectStateValue, IDeployScopedState
{
    [Id(0)] public int TotalInQueue { get; set; }
    [Id(1)] public List<MatchmakingQueueLiveData> Queues { get; set; } = [];

    public int Version => 0;
}

[GenerateSerializer]
public class MatchmakingQueueLiveData
{
    [Id(0)] public string Type { get; set; } = string.Empty;
    [Id(1)] public int Count { get; set; }
    [Id(2)] public double? OldestWaitSeconds { get; set; }
}