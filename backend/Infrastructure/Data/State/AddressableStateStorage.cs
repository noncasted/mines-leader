using Infrastructure.State;

namespace Infrastructure;

public interface IAddressableStateStorage<T> : IGrainWithStringKey
{
    Task Set(T value);
    ValueTask<T> Get();
}

public class AddressableStateStorage<T> : Grain, IAddressableStateStorage<T> where T : class, IStateValue, new()
{
    public AddressableStateStorage([State] State<T> state, IMessaging messaging)
    {
        _state = state;
        _messaging = messaging;
    }

    private readonly State<T> _state;
    private readonly IMessaging _messaging;

    public async Task Set(T value)
    {
        await _state.Replace(value);
        var name = this.GetPrimaryKeyString();

        await _messaging.PushDirectQueue(new AddressableStateMessageQueueId<T>
            {
                Name = name
            }, value!
        );
    }

    public async ValueTask<T> Get()
    {
        return await _state.ReadValue();
    }
}