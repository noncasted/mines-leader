using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.State.Storages;

[GrainState(Table = "state_direct_integer_ext", State = "direct_integer_ext", Lookup = "DirectIntegerExt", Key = GrainKeyType.IntegerAndString)]
public class DirectIntegerExtState : IDirectStateValue
{
    [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public static DirectIntegerExtState Of(int value) => new() { Value = value };
}
