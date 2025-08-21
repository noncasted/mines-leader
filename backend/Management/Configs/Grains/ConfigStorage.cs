using Common;
using Infrastructure.Messaging;

namespace Management.Configs;

public class ConfigStorage : Grain, IConfigStorage
{
    public ConfigStorage(
        [States.ConfigStorage] IPersistentState<ConfigStorageState> state,
        IMessaging messaging)
    {
        _state = state;
        _messaging = messaging;
    }

    private readonly IPersistentState<ConfigStorageState> _state;
    private readonly IMessaging _messaging;

    public Task<T> Get<T>()
    {
        return Task.FromResult<T>((T)_state.State.Value!);
    }

    public async Task Set<T>(T value)
    {
        _state.State.Value = value;
        await _state.WriteStateAsync();
        
    }
}