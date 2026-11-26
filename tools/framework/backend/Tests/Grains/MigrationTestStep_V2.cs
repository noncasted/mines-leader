using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

public class MigrationTestStep_V2 : IStateMigrationStep
{
    public int Version => 2;
    public Type Type => typeof(MigrationTestState_2);

    public IStateValue Deserialize(string raw)
    {
        throw new NotSupportedException("V2 is the latest version; deserialize goes through V1.");
    }

    public IStateValue Migrate(IStateValue value)
    {
        var v1 = (MigrationTestState_1)value;

        return new MigrationTestState_2
        {
            Value = v1.Value,
            Label = v1.Label,
            DoubledValue = v1.Value * 2
        };
    }
}
