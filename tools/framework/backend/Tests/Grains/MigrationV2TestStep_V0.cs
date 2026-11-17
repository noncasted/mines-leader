using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

// A step of V0 that targets MigrationTestState_2, the new final type.
public class MigrationV2TestStep_V0 : IStateMigrationStep
{
    public MigrationV2TestStep_V0(IStateSerializer serializer)
    {
        _serializer = serializer;
    }

    private readonly IStateSerializer _serializer;

    public int Version => 0;
    public Type Type => typeof(MigrationTestState_2);

    public IStateValue Deserialize(string raw)
    {
        return _serializer.TryDeserialize<MigrationTestState_0>(raw)!;
    }

    public IStateValue Migrate(IStateValue value)
    {
        throw new NotSupportedException("V0 step is never a migration target.");
    }
}
