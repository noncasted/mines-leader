using Cluster.Deploy;
using Infrastructure;
using Infrastructure.State;

namespace Cluster.State;

// Keyed by deploy id. A non-reentrant grain serializes read-modify-write of the deploy row across all
// services: concurrent patches of different flags no longer overwrite each other.
public class ClusterFeaturesWriter : Grain, IClusterFeaturesWriter
{
    public ClusterFeaturesWriter(IOrleans orleans, IMessaging messaging)
    {
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;

    public static StateIdentity CreateStateIdentity(IOrleans orleans, Guid deployId)
    {
        var stateInfo = orleans.StateStorage.Registry.Get<ClusterFeaturesState>();

        return new StateIdentity
        {
            Key = deployId,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };
    }

    public Task<AddressableStateValue> Apply(ClusterFeaturesPatch patch)
    {
        var deployId = this.GetPrimaryKey();
        var identity = CreateStateIdentity(_orleans, deployId);

        return DeploymentStateRow.Change<ClusterFeaturesState>(_orleans, _messaging, deployId, identity, value => {
            patch.ApplyTo(value);
            return Task.CompletedTask;
        });
    }
}
