using System.Collections.Concurrent;
using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDurableQueueClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task Listen<T>(IReadOnlyLifetime lifetime, IDurableQueueId id, Func<T, Task> listener);
    void PushTransactional(IDurableQueueId id, object message);
    Task PushDirect(IDurableQueueId id, object message);
}

public class DurableQueueClient : IDurableQueueClient
{
    public DurableQueueClient(
        IOrleans orleans,
        ISideEffectsStorage sideEffectsStorage,
        ILogger<DurableQueueClient> logger)
    {
        _orleans = orleans;
        _sideEffectsStorage = sideEffectsStorage;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly ILogger<DurableQueueClient> _logger;

    private readonly ConcurrentDictionary<string, Listener> _listeners = new();
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop.Run(lifetime,
            () => _listeners.Values.ToArray(),
            () => TimeSpan.FromSeconds(10),
            () => ResubscribeLoop.DefaultTimeout).NoAwait();
        return Task.CompletedTask;
    }

    public async Task Listen<T>(IReadOnlyLifetime lifetime, IDurableQueueId id, Func<T, Task> listener)
    {
        if (lifetime.IsTerminated)
            return;

        var rawId = id.ToRaw();
        var (transportListener, handlerId, created) = await AddToListener(id, async message => {
            if (message is not T castedMessage)
                throw new InvalidCastException($"Expected {typeof(T)}, but got {message.GetType()}");

            await listener(castedMessage);
        });

        lifetime.Listen(() => {
            if (transportListener.RemoveHandler(handlerId))
                RemoveListener(rawId, transportListener);
        });

        if (created == false)
            return;

        try
        {
            await transportListener.Resubscribe();
            transportListener.RecordSuccess();
        }
        catch (Exception e)
        {
            transportListener.RecordFailure(e);

            if (transportListener.RemoveHandler(handlerId))
                RemoveListener(rawId, transportListener);

            throw;
        }
    }

    public void PushTransactional(IDurableQueueId id, object message)
    {
        var transaction = TransactionContextProvider.Current
            ?? throw new InvalidOperationException($"Transactional push to queue {id.ToRaw()} outside of a transaction");

        var sideEffect = new DurableQueueSideEffect()
        {
            QueueName = id.ToRaw(),
            Message = message,
            CorrelationId = transaction.Id
        };

        sideEffect.AddToTransaction();
    }

    public Task PushDirect(IDurableQueueId id, object message)
    {
        return _sideEffectsStorage.Write(new DurableQueueSideEffect()
        {
            QueueName = id.ToRaw(),
            Message = message,
            CorrelationId = Guid.NewGuid()
        });
    }

    private async Task<(Listener Listener, Guid HandlerId, bool Created)> AddToListener(
        IDurableQueueId id,
        Func<object, Task> handler)
    {
        while (true)
        {
            var (listener, created) = await GetOrCreateListener(id);
            var handlerId = listener.AddHandler(handler);

            if (handlerId != null)
                return (listener, handlerId.Value, created);

            // Its last handler has just gone and its removal may not have come yet: the next round finds a new one.
            RemoveListener(id.ToRaw(), listener);
        }
    }

    private async Task<(Listener Listener, bool Created)> GetOrCreateListener(IDurableQueueId id)
    {
        var rawId = id.ToRaw();

        if (_listeners.TryGetValue(rawId, out var existing))
            return (existing, false);

        await _createLock.WaitAsync();

        try
        {
            if (_listeners.TryGetValue(rawId, out existing))
                return (existing, false);

            var listener = new Listener(id, GetQueue(id), _orleans, _logger);
            _listeners[rawId] = listener;
            return (listener, true);
        }
        finally
        {
            _createLock.Release();
        }
    }

    private void RemoveListener(string rawId, Listener listener)
    {
        var listeners = (ICollection<KeyValuePair<string, Listener>>)_listeners;

        if (listeners.Remove(new KeyValuePair<string, Listener>(rawId, listener)))
            listener.Cleanup();
    }

    private IDurableQueue GetQueue(IDurableQueueId id)
    {
        var rawId = id.ToRaw();
        return _orleans.GetGrain<IDurableQueue>(rawId);
    }

    public class Listener : IResubscribingListener
    {
        public Listener(
            IDurableQueueId id,
            IDurableQueue queue,
            IOrleans orleans,
            ILogger logger)
        {
            Id = id;
            Queue = queue;
            Orleans = orleans;
            Logger = logger;
            _failures = new ResubscribeFailures(
                new AdaptiveInterval(minInterval: TimeSpan.FromSeconds(10),
                    maxInterval: TimeSpan.FromSeconds(60),
                    failureBaseInterval: TimeSpan.FromSeconds(1)),
                LogResubscribeFailure);
            _handlers = new LocalHandlers("durable queue", id.ToRaw(), LogHandlerFailure);
            ObserverSource = new DurableQueueObserver(_handlers.Deliver);
            ObserverReference = Orleans.Client.CreateObjectReference<IDurableQueueObserver>(ObserverSource);
        }

        private readonly ResubscribeFailures _failures;
        private readonly LocalHandlers _handlers;

        private int _cleanupStarted;

        public IDurableQueueId Id { get; }
        public DurableQueueObserver ObserverSource { get; }
        public IDurableQueueObserver ObserverReference { get; }
        public IDurableQueue Queue { get; }
        public ILogger Logger { get; }
        public IOrleans Orleans { get; }

        public AdaptiveInterval Interval => _failures.Interval;

        // Null when the listener is on its way out.
        public Guid? AddHandler(Func<object, Task> handler)
        {
            return _handlers.Add(handler);
        }

        public bool RemoveHandler(Guid id)
        {
            return _handlers.Remove(id);
        }

        public async Task Resubscribe()
        {
            await Queue.AddObserver(ObserverSource.Id, ObserverReference);
        }

        public void RecordSuccess()
        {
            _failures.RecordSuccess();
        }

        public void RecordFailure(Exception e)
        {
            _failures.RecordFailure(e);
        }

        public void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupStarted, 1) == 1)
                return;

            CleanupTransport().NoAwait();
        }

        private void LogResubscribeFailure(Exception e, int attempt)
        {
            Logger.LogError(e,
                "[Messaging] [DurableQueue] Failed to rebind observer {ObserverId} (attempt {Count}) to queue {QueueId}",
                ObserverSource.Id, attempt, Id.ToRaw());
        }

        private void LogHandlerFailure(Exception e, Guid handlerId)
        {
            Logger.LogError(e,
                "[Messaging] [DurableQueue] Local handler {HandlerId} failed for queue {QueueId}",
                handlerId, Id.ToRaw());
        }

        private async Task CleanupTransport()
        {
            try
            {
                await Queue.RemoveObserver(ObserverSource.Id);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [DurableQueue] Failed to remove observer {ObserverId} from queue {QueueId}",
                    ObserverSource.Id, Id.ToRaw());
            }

            try
            {
                Orleans.Client.DeleteObjectReference<IDurableQueueObserver>(ObserverReference);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [DurableQueue] Failed to delete object reference for observer {ObserverId} on queue {QueueId}",
                    ObserverSource.Id, Id.ToRaw());
            }
        }
    }
}
