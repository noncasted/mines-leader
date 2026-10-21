namespace Cluster.State;

// Switches of this game, stored next to the cluster switches in ClusterFeaturesState. The framework IClusterFlags
// covers only the cluster itself (side effects).
public interface IGameFlags
{
    bool MatchmakingEnabled { get; }
    bool SnapshotDiffGuardEnabled { get; }
}
