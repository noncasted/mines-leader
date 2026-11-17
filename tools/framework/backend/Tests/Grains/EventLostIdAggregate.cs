using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

// Id with a private setter and no [JsonProperty]: the serializer does not restore it, EventStorage throws on the read.
[GrainEventState(State = "event_lost_id_test", Lookup = "EventLostIdTest", Key = GrainKeyType.Guid)]
public class EventLostIdAggregate : IEventStateValue
{
    public string Id { get; private set; } = string.Empty;
    [JsonProperty] public int Counter { get; private set; }
    public int Version => 0;

    public void Apply(LostIdCounterIncremented e) => Counter += e.Amount;
}
