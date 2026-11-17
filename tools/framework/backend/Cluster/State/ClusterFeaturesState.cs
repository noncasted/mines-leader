using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.State;

[GenerateSerializer]
[GrainState(Table = "cluster", State = "cluster_features", Lookup = "ClusterFeatures", Key = GrainKeyType.Guid)]
public class ClusterFeaturesState : IDirectStateValue, IDeployScopedState
{
    [Id(0)] [JsonProperty(Order = 0)] public bool AcceptingConnections { get; private set; } = false;
    [Id(1)] [JsonProperty(Order = 1)] public bool SideEffectsEnabled { get; private set; } = false;

    // Set by gateways while the coordinator heartbeat is stale, cleared by any service once it is fresh
    // again. Kept apart from AcceptingConnections so that recovery does not override the operator switch.
    [Id(2)] [JsonProperty(Order = 2)] public bool CoordinatorUnhealthy { get; private set; } = false;

    // Switches of the game by name, set through IClusterFeatures.SetGameFlag (the console, the coordinator).
    // The orders keep the stored layout of the time the flags were a public property (Json.NET puts fields last).
    [Id(3)] [JsonProperty(nameof(GameFlags), Order = 3)] private Dictionary<string, bool> _gameFlags = new();

    [JsonIgnore] public IReadOnlyDictionary<string, bool> GameFlags => _gameFlags;

    [JsonProperty(Order = 4)] public int Version => 0;

    public void SetAcceptingConnections(bool accepting)
    {
        AcceptingConnections = accepting;
    }

    public void SetSideEffectsEnabled(bool enabled)
    {
        SideEffectsEnabled = enabled;
    }

    public void SetCoordinatorUnhealthy(bool unhealthy)
    {
        CoordinatorUnhealthy = unhealthy;
    }

    public void SetGameFlag(string name, bool enabled)
    {
        _gameFlags[name] = enabled;
    }
}
