using Infrastructure;

namespace Cluster.State;

public interface IClusterFeaturesWriter : IGrainWithGuidKey
{
    Task<AddressableStateValue> Apply(ClusterFeaturesPatch patch);
}
