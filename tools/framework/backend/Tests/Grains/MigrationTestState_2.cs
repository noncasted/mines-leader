using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

[GenerateSerializer]
[GrainState(Table = "state_test_default_state", State = "migration_test_state", Lookup = "StateMigrationTest",
    Key = GrainKeyType.String)]
public class MigrationTestState_2 : IDirectStateValue
{
    [Id(0)] public int Value { get; init; }
    [Id(1)] public string Label { get; init; } = string.Empty;
    [Id(2)] public int DoubledValue { get; init; }
    public int Version => 2;
}
