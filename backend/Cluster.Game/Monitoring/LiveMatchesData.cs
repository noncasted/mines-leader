using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Shared;

namespace Cluster.Monitoring;

// Registered as a deploy-scoped state so that IDeployCleanup removes the rows of old deploys. The rows hold an
// AddressableStateValue (DeploymentState), the state name is the one DeploymentState.CreateDefaultStateIdentity gives.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_LiveMatchesData", Lookup = "LiveMatches", Key = GrainKeyType.Guid)]
public class LiveMatchesData : IDirectStateValue, IDeployScopedState
{
    [Id(0)] public List<LiveMatchEntry> Matches { get; set; } = [];

    public int Version => 0;
}

[GenerateSerializer]
public class LiveMatchEntry
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Type { get; set; } = string.Empty;
    [Id(2)] public int PlayerCount { get; set; }
    [Id(3)] public DateTime CreatedAt { get; set; }
    [Id(4)] public string GameMode { get; set; } = string.Empty;
    [Id(5)] public Guid Player1Id { get; set; }
    [Id(6)] public Guid Player2Id { get; set; }
}