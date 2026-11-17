using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.State.Storages;

[GrainState(Table = "state_direct_integer", State = "direct_integer", Lookup = "DirectInteger", Key = GrainKeyType.Integer)]
public class DirectIntegerState : IDirectStateValue
{
    [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public static DirectIntegerState Of(int value) => new() { Value = value };
}
