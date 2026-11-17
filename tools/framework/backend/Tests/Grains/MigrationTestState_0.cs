using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

[GenerateSerializer]
[GrainState(Table = "state_test_default_state", State = "migration_test_state", Lookup = "StateMigrationTest",
    Key = GrainKeyType.String)]
public class MigrationTestState_0 : IDirectStateValue
{
    [Id(0)] [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public void SetValue(int value)
    {
        Value = value;
    }
}
