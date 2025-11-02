using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public interface IAddressableDictionaryView<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
}

public abstract class AddressableDictionaryView<TKey, TValue> :
    Dictionary<TKey, TValue>,
    ILocalSetupCompleted,
    IAddressableDictionaryView<TKey, TValue>
    where TKey : notnull
{
    public AddressableDictionaryView(IMessaging messaging)
    {
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;

    protected abstract string Name { get; }

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        var queueId = new AddressableDictionaryMessageQueueId(Name);

        return _messaging.ListenQueue<AddressableDictionaryUpdateMessage<TKey, TValue>>(
            lifetime,
            queueId,
            OnUpdate
        );
    }

    private void OnUpdate(AddressableDictionaryUpdateMessage<TKey, TValue> message)
    {
        foreach (var update in message.Updates)
            this[update.Key] = update.Value;

        foreach (var removal in message.Removals)
            Remove(removal);
    }
}

public static class AddressableDictionaryViewExtensions
{
    public static ContainerExtensions.Registration AddAddressableDictionaryView<TInterface, TImplementation>(
        this IHostApplicationBuilder builder)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        return builder.Services.Add<TInterface, TImplementation>()
            .AsSetupLoopStage();
    }
}