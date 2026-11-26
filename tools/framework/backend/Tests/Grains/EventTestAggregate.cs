using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

[GenerateSerializer]
[GrainEventState(State = "event_test", Lookup = "EventTest", Key = GrainKeyType.Guid)]
public class EventTestAggregate : IEventStateValue
{
    [Id(0)] [JsonProperty] public string Id { get; private set; } = string.Empty;
    [Id(1)] [JsonProperty] public int Counter { get; private set; }
    [Id(2)] [JsonProperty] public string Label { get; private set; } = string.Empty;
    public int Version => 0;

    public void Apply(CounterIncremented e) => Counter += e.Amount;
    public void Apply(LabelChanged e) => Label = e.Label;
}
