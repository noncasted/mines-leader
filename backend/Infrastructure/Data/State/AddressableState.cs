using Common.Extensions;
using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

[GenerateSerializer]
public class AddressableStateValue : IStateValue
{
    [Id(0)]
    public string Value { get; set; } = string.Empty;

    [Id(1)]
    public bool IsInitialized { get; set; }

    [Id(2)]
    public DateTime UpdateDate { get; set; }

    public int Version => 0;
}

public interface IAddressableState<T> : IViewableProperty<T> where T : class, new()
{
    bool IsInitialized { get; }

    Task SetValue(T value);
}

public class AddressableStateMessageQueueId<T> : IMessageQueueId
{
    public required string Name { get; init; }

    public string ToRaw()
    {
        return $"addressable-state-{Name}";
    }
}

public class AddressableState<T> : ViewableProperty<T>, IOrleansStarted, IAddressableState<T>
    where T : class, new()
{
    public AddressableState(IOrleans orleans, IMessaging messaging) : base(new T())
    {
        _orleans = orleans;
        _messaging = messaging;

        var stateInfo = _orleans.StateStorage.Registry.Get<T>();

        _identity = new StateIdentity
        {
            Key = stateInfo.Name,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        _queueId = new AddressableStateMessageQueueId<T>
        {
            Name = _identity.Type
        };
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly StateIdentity _identity;
    private readonly AddressableStateMessageQueueId<T> _queueId;

    private bool _isInitialized;
    private DateTime _updateDate;

    public bool IsInitialized => _isInitialized;
    public DateTime UpdateDate => _updateDate;

    public async Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        await _messaging.ListenQueue<AddressableStateValue>(lifetime, _queueId, OnUpdate);

        var state = await _orleans.StateStorage.Read<AddressableStateValue>(_identity);

        if (state.IsInitialized == false)
            return;

        var value = _orleans.Serializer.Deserialize<T>(state.Value);
        Set(value);
    }

    private void OnUpdate(AddressableStateValue state)
    {
        _isInitialized = true;
        _updateDate = DateTime.UtcNow;
        var value = _orleans.Serializer.Deserialize<T>(state.Value);
        Set(value);
    }


    public async Task SetValue(T value)
    {
        _isInitialized = true;
        _updateDate = DateTime.UtcNow;

        Set(value);

        var state = new AddressableStateValue()
        {
            IsInitialized = true,
            UpdateDate = _updateDate,
            Value = _orleans.Serializer.Serialize(value)
        };

        await _orleans.StateStorage.Write(_identity, state);
        await _messaging.Queue.PushDirect(_queueId, state);
    }
}

public static class AddressableStateExtensions
{
    public static ContainerExtensions.Registration AddAddressableState<T>(this IHostApplicationBuilder builder)
        where T : class
    {
        return builder.Add<T>()
            .As<IOrleansStarted>();
    }
}