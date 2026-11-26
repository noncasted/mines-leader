using Common;
using Cluster.Deploy;
using Shared;

namespace Cluster.State;

public interface IClusterFeatures : IDeploymentState<ClusterFeaturesState>, IClusterFlags
{
    // Operator / coordinator switch combined with the coordinator health seen by the gateways.
    bool IsAcceptingConnections { get; }

    Task SetAcceptingConnections(bool accepting);
    Task SetSideEffectsEnabled(bool enabled);
    Task SetCoordinatorUnhealthy(bool unhealthy);

    // Switches of the game, by name (the game names them; an unknown name is off).
    bool IsGameFlagEnabled(string name);
    Task SetGameFlag(string name, bool enabled);
}
