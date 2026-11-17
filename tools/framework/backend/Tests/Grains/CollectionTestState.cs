using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

[GenerateSerializer]
[GrainState(Table = "state_test_collection", State = "collection_test", Lookup = "CollectionTest",
    Key = GrainKeyType.Guid)]
public class CollectionTestState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public Guid Id { get; private set; }
    [Id(1)] [JsonProperty] public string Name { get; private set; } = string.Empty;
    public int Version => 0;

    public void Init(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}
