using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimeChannelClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task<IViewableDelegate<T>> GetOrCreateConsumer<T>(IRuntimeChannelId id);
    Task Publish(IRuntimeChannelId id, object message);
}

public class RuntimeChannelClient : IRuntimeChannelClient
{
    public RuntimeChannelClient(
        IOrleans orleans,
        ILogger<RuntimeChannelClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ILogger<RuntimeChannelClient> _logger;

    private readonly ConcurrentDictionary<string, Listener> _listeners = new();

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task<IViewableDelegate<T>> GetOrCreateConsumer<T>(IRuntimeChannelId id)
    {
        var rawId = id.ToRaw();

        if (_listeners.TryGetValue(rawId, out var existing))
            return (ViewableDelegate<T>)existing.Delegate;

        var source = new ViewableDelegate<T>();

        var observer = new RuntimeChannelObserver(message =>
            {
                if (message is not T castedMessage)
                    throw new InvalidCastException($"Expected {typeof(T)}, but got {message.GetType()}");

                source.Invoke(castedMessage);
            }
        );

        var observerReference = _orleans.Client.CreateObjectReference<IRuntimeChannelObserver>(observer);

        var listener = new Listener
        {
            Id = id,
            ObserverSource = observer,
            ObserverReference = observerReference,
            Channel = GetChannel(id),
            Logger = _logger,
            Delegate = source
        };

        _listeners[rawId] = listener;
        await listener.Resubscribe();

        return source;
    }

    public Task Publish(IRuntimeChannelId id, object message)
    {
        return GetChannel(id).Publish(message);
    }

    private IRuntimeChannel GetChannel(IRuntimeChannelId id)
    {
        return _orleans.GetGrain<IRuntimeChannel>(id.ToRaw());
    }

    private async Task ResubscribeLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            await Task.WhenAll(_listeners.Select(t => t.Value.Resubscribe()));
            await Task.Delay(TimeSpan.FromSeconds(10), lifetime.Token);
        }
    }

    public class Listener
    {
        public required IRuntimeChannelId Id { get; init; }
        public required RuntimeChannelObserver ObserverSource { get; init; }
        public required IRuntimeChannelObserver ObserverReference { get; init; }
        public required IRuntimeChannel Channel { get; init; }
        public required ILogger Logger { get; init; }
        public required object Delegate { get; init; }

        public Task Resubscribe()
        {
            try
            {
                return Channel.AddObserver(ObserverSource.Id, ObserverReference);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "[Messaging] [Channel] Failed to rebind observer to channel {ChannelId}",
                    Id.ToRaw()
                );

                return Task.CompletedTask;
            }
        }
    }
}