using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

public class MigrationV2TestStep_V1 : IStateMigrationStep
{
    public MigrationV2TestStep_V1(IStateSerializer serializer)
    {
        _serializer = serializer;
    }

    private readonly IStateSerializer _serializer;

    public int Version => 1;
    public Type Type => typeof(MigrationTestState_2);

    public IStateValue Deserialize(string raw)
    {
        return _serializer.TryDeserialize<MigrationTestState_1>(raw)!;
    }

    public IStateValue Migrate(IStateValue value)
    {
        var v0 = (MigrationTestState_0)value;

        return new MigrationTestState_1
        {
            Value = v0.Value,
            Label = $"migrated-{v0.Value}"
        };
    }
}
