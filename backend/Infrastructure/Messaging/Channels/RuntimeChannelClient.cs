using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimeChannelClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task Listen<T>(
        IReadOnlyLifetime lifetime,
        IRuntimeChannelId id,
        Func<T, Task> listener,
        Action? onGapDetected = null);

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
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task Listen<T>(
        IReadOnlyLifetime lifetime,
        IRuntimeChannelId id,
        Func<T, Task> listener,
        Action? onGapDetected = null)
    {
        if (lifetime.IsTerminated)
            return;

        var rawId = id.ToRaw();
        var (transportListener, created) = await GetOrCreateListener(id);
        var handlerId = transportListener.AddHandler(async message => {
            if (message is not T castedMessage)
                throw new InvalidCastException($"Expected {typeof(T)}, but got {message.GetType()}");

            await listener(castedMessage);
        }, onGapDetected);

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

    public Task Publish(IRuntimeChannelId id, object message)
    {
        return GetChannel(id).Publish(message);
    }

    private async Task<(Listener Listener, bool Created)> GetOrCreateListener(IRuntimeChannelId id)
    {
        var rawId = id.ToRaw();

        if (_listeners.TryGetValue(rawId, out var existing))
            return (existing, false);

        await _createLock.WaitAsync();

        try
        {
            if (_listeners.TryGetValue(rawId, out existing))
                return (existing, false);

            var listener = new Listener(id, GetChannel(id), _orleans, _logger);
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

    private IRuntimeChannel GetChannel(IRuntimeChannelId id)
    {
        return _orleans.GetGrain<IRuntimeChannel>(id.ToRaw());
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
            IRuntimeChannelId id,
            IRuntimeChannel channel,
            IOrleans orleans,
            ILogger logger)
        {
            Id = id;
            Channel = channel;
            Orleans = orleans;
            Logger = logger;
            ObserverSource = new RuntimeChannelObserver(Deliver);
            ObserverReference = Orleans.Client.CreateObjectReference<IRuntimeChannelObserver>(ObserverSource);
        }

        private readonly object _gate = new();
        private readonly Dictionary<Guid, HandlerData> _handlers = new();

        private int _cleanupStarted;
        private int _consecutiveFailures;

        public IRuntimeChannelId Id { get; }
        public RuntimeChannelObserver ObserverSource { get; }
        public IRuntimeChannelObserver ObserverReference { get; }
        public IRuntimeChannel Channel { get; }
        public ILogger Logger { get; }
        public IOrleans Orleans { get; }

        public AdaptiveInterval Interval { get; } = new(minInterval: TimeSpan.FromSeconds(10),
            maxInterval: TimeSpan.FromSeconds(60),
            failureBaseInterval: TimeSpan.FromSeconds(1));

        public Guid AddHandler(Func<object, Task> handler, Action? onGapDetected)
        {
            var id = Guid.NewGuid();

            lock (_gate)
            {
                _handlers[id] = new HandlerData
                {
                    Handler = handler,
                    OnGapDetected = onGapDetected
                };
            }

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
            ObserverSource.BeginBuffering();
            var lastSeenSequence = ObserverSource.LastSeenSequence;

            try
            {
                await Channel.AddObserver(ObserverSource.Id, ObserverReference);

                if (lastSeenSequence == 0)
                    return;

                var catchUp = await Channel.CatchUp(lastSeenSequence);

                if (catchUp.GapDetected)
                {
                    Logger.LogWarning(
                        "[Messaging] [Channel] Gap detected on {ChannelId}, last seen seq {LastSeq}, current seq {CurrentSeq}, replaying {Count} messages",
                        Id.ToRaw(), lastSeenSequence, catchUp.CurrentSequence, catchUp.Messages.Count);

                    if (lastSeenSequence > catchUp.CurrentSequence)
                        ObserverSource.ResetLastSeen(0);

                    InvokeGapDetected();
                }

                await ObserverSource.ReplayCatchUp(catchUp.Messages);
            }
            finally
            {
                await ObserverSource.EndBuffering();
            }
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
                    "[Messaging] [Channel] Failed to rebind observer {ObserverId} (attempt {Count}) to channel {ChannelId}",
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
            KeyValuePair<Guid, HandlerData>[] handlers;

            lock (_gate)
                handlers = _handlers.ToArray();

            if (handlers.Length == 0)
                throw new InvalidOperationException($"No local channel handlers for {Id.ToRaw()}");

            var successCount = 0;
            List<Exception>? failures = null;

            foreach (var (handlerId, data) in handlers)
            {
                try
                {
                    await data.Handler(message);
                    successCount++;
                }
                catch (Exception e)
                {
                    failures ??= [];
                    failures.Add(e);

                    Logger.LogError(e,
                        "[Messaging] [Channel] Local handler {HandlerId} failed for channel {ChannelId}",
                        handlerId, Id.ToRaw());
                }
            }

            if (successCount == 0 && failures != null)
                throw new AggregateException($"All local channel handlers failed for {Id.ToRaw()}", failures);
        }

        private void InvokeGapDetected()
        {
            HandlerData[] handlers;

            lock (_gate)
                handlers = _handlers.Values.ToArray();

            foreach (var handler in handlers)
            {
                try
                {
                    handler.OnGapDetected?.Invoke();
                }
                catch (Exception e)
                {
                    Logger.LogError(e,
                        "[Messaging] [Channel] Gap handler failed for channel {ChannelId}",
                        Id.ToRaw());
                }
            }
        }

        private async Task CleanupTransport()
        {
            try
            {
                await Channel.RemoveObserver(ObserverSource.Id);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [Channel] Failed to remove observer {ObserverId} from channel {ChannelId}",
                    ObserverSource.Id, Id.ToRaw());
            }

            try
            {
                Orleans.Client.DeleteObjectReference<IRuntimeChannelObserver>(ObserverReference);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [Channel] Failed to delete object reference for observer {ObserverId} on channel {ChannelId}",
                    ObserverSource.Id, Id.ToRaw());
            }
        }

        private class HandlerData
        {
            public required Func<object, Task> Handler { get; init; }
            public Action? OnGapDetected { get; init; }
        }
    }
}
