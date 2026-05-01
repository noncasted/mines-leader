using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
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
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task Listen<T>(IReadOnlyLifetime lifetime, IDurableQueueId id, Func<T, Task> listener)
    {
        if (lifetime.IsTerminated)
            return;

        var rawId = id.ToRaw();
        var (transportListener, created) = await GetOrCreateListener(id);
        var handlerId = transportListener.AddHandler(async message => {
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
        if (TransactionContextProvider.Current == null)
            throw new InvalidOperationException();

        var sideEffect = new DurableQueueSideEffect()
        {
            QueueName = id.ToRaw(),
            Message = message,
            CorrelationId = TransactionContextProvider.Current?.Id ?? Guid.NewGuid()
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

    private async Task ResubscribeLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            var snapshot = _listeners.Values.ToArray();

            foreach (var listener in snapshot)
            {
                try
                {
                    await listener.Resubscribe();
                    listener.RecordSuccess();
                }
                catch (Exception e)
                {
                    listener.RecordFailure(e);
                }
            }

            var delay = snapshot.Length == 0
                ? TimeSpan.FromSeconds(10)
                : snapshot.Min(l => l.Interval.GetNextDelay());

            try
            {
                await Task.Delay(delay, lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated)
            {
            }
        }
    }

    public class Listener
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
            ObserverSource = new DurableQueueObserver(Deliver);
            ObserverReference = Orleans.Client.CreateObjectReference<IDurableQueueObserver>(ObserverSource);
        }

        private readonly object _gate = new();
        private readonly Dictionary<Guid, Func<object, Task>> _handlers = new();

        private int _cleanupStarted;
        private int _consecutiveFailures;

        public IDurableQueueId Id { get; }
        public DurableQueueObserver ObserverSource { get; }
        public IDurableQueueObserver ObserverReference { get; }
        public IDurableQueue Queue { get; }
        public ILogger Logger { get; }
        public IOrleans Orleans { get; }

        public AdaptiveInterval Interval { get; } = new(minInterval: TimeSpan.FromSeconds(10),
            maxInterval: TimeSpan.FromSeconds(60),
            failureBaseInterval: TimeSpan.FromSeconds(1));

        public Guid AddHandler(Func<object, Task> handler)
        {
            var id = Guid.NewGuid();

            lock (_gate)
                _handlers[id] = handler;

            return id;
        }

        public bool RemoveHandler(Guid id)
        {
            lock (_gate)
            {
                _handlers.Remove(id);
                return _handlers.Count == 0;
            }
        }

        public async Task Resubscribe()
        {
            await Queue.AddObserver(ObserverSource.Id, ObserverReference);
        }

        public void RecordSuccess()
        {
            _consecutiveFailures = 0;
            Interval.RecordSuccess();
        }

        public void RecordFailure(Exception e)
        {
            _consecutiveFailures++;
            Interval.RecordFailure();

            if (_consecutiveFailures == 1 || _consecutiveFailures % 10 == 0)
            {
                Logger.LogError(e,
                    "[Messaging] [DurableQueue] Failed to rebind observer {ObserverId} (attempt {Count}) to queue {QueueId}",
                    ObserverSource.Id, _consecutiveFailures, Id.ToRaw());
            }
        }

        public void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupStarted, 1) == 1)
                return;

            CleanupTransport().NoAwait();
        }

        private async Task Deliver(object message)
        {
            KeyValuePair<Guid, Func<object, Task>>[] handlers;

            lock (_gate)
                handlers = _handlers.ToArray();

            if (handlers.Length == 0)
                throw new InvalidOperationException($"No local durable queue handlers for {Id.ToRaw()}");

            var successCount = 0;
            List<Exception>? failures = null;

            foreach (var (handlerId, handler) in handlers)
            {
                try
                {
                    await handler(message);
                    successCount++;
                }
                catch (Exception e)
                {
                    failures ??= [];
                    failures.Add(e);

                    Logger.LogError(e,
                        "[Messaging] [DurableQueue] Local handler {HandlerId} failed for queue {QueueId}",
                        handlerId, Id.ToRaw());
                }
            }

            if (successCount == 0 && failures != null)
                throw new AggregateException($"All local durable queue handlers failed for {Id.ToRaw()}", failures);
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
