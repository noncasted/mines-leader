using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Shared;

namespace Cluster.Monitoring;

// Registered as a deploy-scoped state so that IDeployCleanup removes the rows of old deploys. The rows hold an
// AddressableStateValue (DeploymentState), the state name is the one DeploymentState.CreateDefaultStateIdentity gives.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_ConnectedUsersLiveData", Lookup = "ConnectedUsersLive", Key = GrainKeyType.Guid)]
public class ConnectedUsersLiveData : IDirectStateValue, IDeployScopedState
{
    [Id(0)] public int Count { get; set; }
    [Id(1)] public List<ConnectedUserEntry> Users { get; set; } = [];

    public int Version => 0;
}

[GenerateSerializer]
public class ConnectedUserEntry
{
    [Id(0)] public Guid UserId { get; set; }
}