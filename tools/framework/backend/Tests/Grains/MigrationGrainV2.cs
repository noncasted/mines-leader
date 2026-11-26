using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

public interface IMigrationGrainV2 : IGrainWithStringKey
{
    Task<(int value, string label, int doubledValue)> Read();
}

public class MigrationGrainV2 : Grain, IMigrationGrainV2
{
    public MigrationGrainV2([State] State<MigrationTestState_2> state)
    {
        _state = state;
    }

    private readonly State<MigrationTestState_2> _state;

    public async Task<(int value, string label, int doubledValue)> Read()
    {
        await _state.Read();
        return (_state.Value.Value, _state.Value.Label, _state.Value.DoubledValue);
    }
}
