using Cluster.Discovery;
using Infrastructure;
using Infrastructure.State;

namespace Cluster.Deploy;

// Keyed by deploy id. A non-reentrant grain serializes read-modify-write of the deploy row across services,
// like ClusterFeaturesWriter.
public class ServiceSlicesWriter<TData, TSlice> : Grain, IServiceSlicesWriter<TData, TSlice>
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    public ServiceSlicesWriter(IOrleans orleans, IMessaging messaging)
    {
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;

    public Task<AddressableStateValue> SetSlice(Guid serviceId, TSlice slice)
    {
        var deployId = this.GetPrimaryKey();
        var identity = DeploymentState<TData>.CreateDefaultStateIdentity(deployId);

        return DeploymentStateRow.Change<TData>(_orleans, _messaging, deployId, identity, async value => {
            // A service that stopped without clearing its slice (crash) would stay in the data for the whole deploy:
            // drop slices of services that are no longer alive in discovery.
            var members = await _orleans.GetGrain<IServiceDiscoveryStorage>(deployId).Get();
            var aliveSince = DateTime.UtcNow - ServiceDiscoveryStorage.StaleThreshold;

            foreach (var id in value.Slices.Keys.ToList())
            {
                if (id == serviceId)
                    continue;

                if (members.TryGetValue(id, out var member) == false || member.UpdateTime < aliveSince)
                    value.RemoveSlice(id);
            }

            value.SetSlice(serviceId, slice);
        });
    }
}
