using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.State.Storages;

[GrainState(Table = "state_direct_guid", State = "direct_guid", Lookup = "DirectGuid", Key = GrainKeyType.Guid)]
public class DirectGuidState : IDirectStateValue
{
    [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public static DirectGuidState Of(int value) => new() { Value = value };
}
