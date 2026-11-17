using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Discovery;

[GenerateSerializer]
[GrainState(Table = "cluster", State = "service_discovery", Lookup = "ServiceDiscovery", Key = GrainKeyType.Guid)]
public class ServiceDiscoveryStorageState : IDirectStateValue, IDeployScopedState
{
    [Id(0)] [JsonProperty(nameof(Members))] private Dictionary<Guid, IServiceOverview> _members = new();

    [JsonIgnore] public IReadOnlyDictionary<Guid, IServiceOverview> Members => _members;

    public int Version => 0;

    public void Set(IServiceOverview overview)
    {
        _members[overview.Id] = overview;
    }

    public void Remove(Guid id)
    {
        _members.Remove(id);
    }
}
