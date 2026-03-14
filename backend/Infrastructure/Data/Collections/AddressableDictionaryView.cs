using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public interface IAddressableDictionaryView<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    IViewableDelegate Updated { get; }
}

public abstract class AddressableDictionaryView<TKey, TValue, TGrain> :
    Dictionary<TKey, TValue>,
    ILocalSetupCompleted,
    IAddressableDictionaryView<TKey, TValue>
    where TKey : notnull
    where TGrain : IAddressableDictionary<TKey, TValue>
{
    public AddressableDictionaryView(IOrleans orleans, IMessaging messaging)
    {
        _orleans = orleans;
        _messaging = messaging;
        _queueId = new AddressableDictionaryMessageQueueId($"{typeof(TKey)}-{typeof(TValue)}");
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly AddressableDictionaryMessageQueueId _queueId;
    private readonly ViewableDelegate _updated = new();

    public IViewableDelegate Updated => _updated;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        var grain = _orleans.GetGrain<TGrain>(Guid.Empty);
        var entries = await grain.GetAll();

        foreach (var (key, value) in entries)
            this[key] = value;
        
        _updated.Invoke();

        await _messaging.ListenQueue<AddressableDictionaryUpdateMessage<TKey, TValue>>(
            lifetime,
            _queueId,
            OnUpdate
        );
    }

    private void OnUpdate(AddressableDictionaryUpdateMessage<TKey, TValue> message)
    {
        foreach (var update in message.Updates)
            this[update.Key] = update.Value;

        foreach (var removal in message.Removals)
            Remove(removal);
        
        _updated.Invoke();
    }
}

public static class AddressableDictionaryViewExtensions
{
    public static ContainerExtensions.Registration AddAddressableDictionaryView<TInterface, TImplementation>(
        this IHostApplicationBuilder builder)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        return builder.Add<TImplementation>()
            .As<TInterface>()
            .AsSetupLoopStage();
    }
}