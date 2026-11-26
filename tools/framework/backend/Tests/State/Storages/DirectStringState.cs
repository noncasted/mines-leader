using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.State.Storages;

[GrainState(Table = "state_direct_string", State = "direct_string", Lookup = "DirectString", Key = GrainKeyType.String)]
public class DirectStringState : IDirectStateValue
{
    [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public static DirectStringState Of(int value) => new() { Value = value };
}
