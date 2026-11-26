using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Monitoring;

// Users connected to every meta gateway, one slice per gateway service id (written via IServiceSlicedState).
// Registered as a deploy-scoped state so that IDeployCleanup removes the rows of old deploys. The rows hold an
// AddressableStateValue (DeploymentState), the state name is the one DeploymentState.CreateDefaultStateIdentity gives.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_ConnectedUsersLiveData", Lookup = "ConnectedUsersLive", Key = GrainKeyType.Guid)]
public class ConnectedUsersLiveData : IServiceSlices<List<ConnectedUserEntry>>, IDirectStateValue, IDeployScopedState
{
    // Id 0, 1 removed, kept for stored values.
    [Id(2)] [JsonProperty(nameof(Slices))] private Dictionary<Guid, List<ConnectedUserEntry>> _slices = new();

    [JsonIgnore] public IReadOnlyDictionary<Guid, List<ConnectedUserEntry>> Slices => _slices;

    public int Version => 0;

    public void SetSlice(Guid serviceId, List<ConnectedUserEntry> slice)
    {
        _slices[serviceId] = slice;
    }

    public void RemoveSlice(Guid serviceId)
    {
        _slices.Remove(serviceId);
    }

    public List<ConnectedUserEntry> AllUsers()
    {
        return Slices.Values.SelectMany(slice => slice).ToList();
    }
}
