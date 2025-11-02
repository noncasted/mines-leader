using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IMessageQueueClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task<IViewableDelegate<T>> GetOrCreateConsumer<T>(IMessageQueueId id);
    Task PushTransactional(IMessageQueueId id, object message);
    Task PushDirect(IMessageQueueId id, object message);
}

public class MessageQueueClient : IMessageQueueClient
{
    public MessageQueueClient(IOrleans orleans, ILogger<MessageQueueClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly ILogger<MessageQueueClient> _logger;

    private readonly ConcurrentDictionary<string, Listener> _listeners = new();

    private readonly IOrleans _orleans;

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task<IViewableDelegate<T>> GetOrCreateConsumer<T>(IMessageQueueId id)
    {
        var rawId = id.ToRaw();

        if (_listeners.TryGetValue(rawId, out var existing) == true)
            return (ViewableDelegate<T>)existing.Delegate;

        var source = new ViewableDelegate<T>();

        var observer = new MessageQueueObserver(message =>
            {
                if (message is not T castedMessage)
                    throw new InvalidCastException();

                source.Invoke(castedMessage);
            }
        );

        var observerReference = _orleans.Client.CreateObjectReference<IMessageQueueObserver>(observer);

        var listener = new Listener
        {
            Id = id,
            ObserverSource = observer,
            ObserverReference = observerReference,
            Queue = GetQueue(id
            ),
            Logger = _logger,
            Delegate = source
        };

        _listeners[rawId] = listener;
        await listener.Resubscribe();

        return source;
    }

    public Task PushTransactional(IMessageQueueId id, object message)
    {
        return GetQueue(id).PushTransactional(message);
    }

    public Task PushDirect(IMessageQueueId id, object message)
    {
        return GetQueue(id).PushDirect(message);
    }

    private IMessageQueue GetQueue(IMessageQueueId id)
    {
        var rawId = id.ToRaw();
        return _orleans.GetGrain<IMessageQueue>(rawId);
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
        public required IMessageQueueId Id { get; init; }
        public required MessageQueueObserver ObserverSource { get; init; }
        public required IMessageQueueObserver ObserverReference { get; init; }
        public required IMessageQueue Queue { get; init; }
        public required ILogger Logger { get; init; }
        public required object Delegate { get; init; }

        public Task Resubscribe()
        {
            try
            {
                return Queue.AddObserver(ObserverSource.Id, ObserverReference);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
                    Id.ToRaw()
                );

                return Task.CompletedTask;
            }
        }
    }
}