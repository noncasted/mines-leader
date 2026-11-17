using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

[GenerateSerializer]
[GrainState(Table = "state_simple_test", State = "simple_test", Lookup = "SimpleTest", Key = GrainKeyType.Guid)]
public class SimpleTestState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public int Counter { get; private set; }
    [Id(1)] [JsonProperty] public string Label { get; private set; } = string.Empty;
    public int Version => 0;

    public static SimpleTestState Of(int counter, string label = "")
    {
        return new SimpleTestState { Counter = counter, Label = label };
    }

    public void SetCounter(int value)
    {
        Counter = value;
    }

    public void SetLabel(string label)
    {
        Label = label;
    }
}
