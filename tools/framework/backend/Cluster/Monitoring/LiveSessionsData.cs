using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Monitoring;

// Sessions of every game gateway, one slice per gateway service id (written via IServiceSlicedState).
// Registered as a deploy-scoped state so that IDeployCleanup removes the rows of old deploys. The rows hold an
// AddressableStateValue (DeploymentState), the state name is the one DeploymentState.CreateDefaultStateIdentity gives.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_LiveSessionsData", Lookup = "LiveSessions", Key = GrainKeyType.Guid)]
public class LiveSessionsData : IServiceSlices<List<LiveSessionEntry>>, IDirectStateValue, IDeployScopedState
{
    // Id 0 removed, kept for stored values.
    [Id(1)] [JsonProperty(nameof(Slices))] private Dictionary<Guid, List<LiveSessionEntry>> _slices = new();

    [JsonIgnore] public IReadOnlyDictionary<Guid, List<LiveSessionEntry>> Slices => _slices;

    public int Version => 0;

    public void SetSlice(Guid serviceId, List<LiveSessionEntry> slice)
    {
        _slices[serviceId] = slice;
    }

    public void RemoveSlice(Guid serviceId)
    {
        _slices.Remove(serviceId);
    }
}
