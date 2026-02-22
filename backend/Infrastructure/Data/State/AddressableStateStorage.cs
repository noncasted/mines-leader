namespace Infrastructure;

public interface IAddressableStateStorage<T> : IGrainWithStringKey
{
    Task Set(T value);
    ValueTask<T> Get();
}

public class AddressableStateStorage<T> : Grain, IAddressableStateStorage<T>
{
    public AddressableStateStorage([States.ClusterState] IPersistentState<T> state, IMessaging messaging)
    {
        _state = state;
        _messaging = messaging;
    }

    private readonly IPersistentState<T> _state;
    private readonly IMessaging _messaging;
    
    public Task Set(T value)
    {
        _state.State = value;
        var name = this.GetPrimaryKeyString();

        return Task.WhenAll(
            _state.WriteStateAsync(),
            _messaging.PushDirectQueue(new AddressableStateMessageQueueId<T>
            {
                Name = name
            }, value!)
        );
    }

    public ValueTask<T> Get()
    {
        return ValueTask.FromResult(_state.State);
    }
}