using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.State.Storages;

[GrainState(Table = "state_direct_guid_ext", State = "direct_guid_ext", Lookup = "DirectGuidExt", Key = GrainKeyType.GuidAndString)]
public class DirectGuidExtState : IDirectStateValue
{
    [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public static DirectGuidExtState Of(int value) => new() { Value = value };
}
