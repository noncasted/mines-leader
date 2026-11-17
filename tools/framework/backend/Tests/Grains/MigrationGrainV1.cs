using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Framework.Tests.Grains;

public interface IMigrationGrainV1 : IGrainWithStringKey
{
    Task<(int value, string label)> Read();
}

public class MigrationGrainV1 : Grain, IMigrationGrainV1
{
    public MigrationGrainV1([State] State<MigrationTestState_1> state)
    {
        _state = state;
    }

    private readonly State<MigrationTestState_1> _state;

    public async Task<(int value, string label)> Read()
    {
        await _state.Read();
        return (_state.Value.Value, _state.Value.Label);
    }
}
