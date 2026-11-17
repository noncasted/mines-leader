using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

public class MigrationTestStep_V0 : IStateMigrationStep
{
    public MigrationTestStep_V0(IStateSerializer serializer)
    {
        _serializer = serializer;
    }

    private readonly IStateSerializer _serializer;

    public int Version => 0;
    public Type Type => typeof(MigrationTestState_1);

    public IStateValue Deserialize(string raw)
    {
        return _serializer.TryDeserialize<MigrationTestState_0>(raw)!;
    }

    public IStateValue Migrate(IStateValue value)
    {
        throw new NotSupportedException("V0 step is never a migration target.");
    }
}
