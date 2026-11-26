using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

public interface IMigrationGrainV0 : IGrainWithStringKey
{
    Task Write(int value);
}

public class MigrationGrainV0 : Grain, IMigrationGrainV0
{
    public MigrationGrainV0([State] State<MigrationTestState_0> state)
    {
        _state = state;
    }

    private readonly State<MigrationTestState_0> _state;

    public Task Write(int value)
    {
        return _state.Update(s => s.SetValue(value));
    }
}
