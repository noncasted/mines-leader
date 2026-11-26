using System.Collections.Concurrent;
using Common.Extensions;
using Internal;
using Microsoft.Extensions.DependencyInjection;
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
        IServiceProvider services,
        ILogger<RuntimeChannelClient> logger)
    {
        _orleans = orleans;
        _config = new Lazy<IRuntimeChannelConfig>(() => services.GetRequiredService<IRuntimeChannelConfig>());
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly Lazy<IRuntimeChannelConfig> _config;
    private readonly ILogger<RuntimeChannelClient> _logger;

    private readonly ConcurrentDictionary<Guid, Listener> _listeners = new();

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop.Run(lifetime,
            () => _listeners.Values.ToArray(),
            () => TimeSpan.FromMilliseconds(_config.Value.Value.ResubscribeMinIntervalMs),
            () => TimeSpan.FromMilliseconds(_config.Value.Value.ResubscribeTimeoutMs)).NoAwait();

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

        var transportListener = new Listener(id, GetChannel(id), _orleans, _logger, _config.Value.Value);
        _listeners[transportListener.ObserverSource.Id] = transportListener;

        var handlerId = transportListener.AddHandler(async message => {
            if (message is not T castedMessage)
                throw new InvalidCastException($"Expected {typeof(T)}, but got {message.GetType()}");

            await listener(castedMessage);
        }, onGapDetected);

        lifetime.Listen(() => {
            if (transportListener.RemoveHandler(handlerId))
                RemoveListener(transportListener);
        });

        try
        {
            await transportListener.Resubscribe();
            transportListener.RecordSuccess();
        }
        catch (Exception e)
        {
            transportListener.RecordFailure(e);

            if (transportListener.RemoveHandler(handlerId))
                RemoveListener(transportListener);

            throw;
        }
    }

    public Task Publish(IRuntimeChannelId id, object message)
    {
        return GetChannel(id).Publish(message);
    }

    private void RemoveListener(Listener listener)
    {
        if (_listeners.TryRemove(listener.ObserverSource.Id, out _))
            listener.Cleanup();
    }

    private IRuntimeChannel GetChannel(IRuntimeChannelId id)
    {
        return _orleans.GetGrain<IRuntimeChannel>(id.ToRaw());
    }

    public class Listener : IResubscribingListener
    {
        public Listener(
            IRuntimeChannelId id,
            IRuntimeChannel channel,
            IOrleans orleans,
            ILogger logger,
            RuntimeChannelOptions options)
        {
            Id = id;
            Channel = channel;
            Orleans = orleans;
            Logger = logger;
            _failures = new ResubscribeFailures(
                new AdaptiveInterval(minInterval: TimeSpan.FromMilliseconds(options.ResubscribeMinIntervalMs),
                    maxInterval: TimeSpan.FromMilliseconds(options.ResubscribeMaxIntervalMs),
                    failureBaseInterval: TimeSpan.FromSeconds(1)),
                LogResubscribeFailure);
            _handlers = new LocalHandlers("channel", id.ToRaw(), LogHandlerFailure);
            _gapHandlers = new RuntimeChannelGapHandlers(id, logger);
            ObserverSource = new RuntimeChannelObserver(_handlers.Deliver);
            ObserverReference = Orleans.Client.CreateObjectReference<IRuntimeChannelObserver>(ObserverSource);
            _resubscription = new RuntimeChannelResubscription(id, channel, ObserverSource, ObserverReference,
                _gapHandlers, logger);
        }

        private readonly ResubscribeFailures _failures;
        private readonly LocalHandlers _handlers;
        private readonly RuntimeChannelGapHandlers _gapHandlers;
        private readonly RuntimeChannelResubscription _resubscription;

        private int _cleanupStarted;

        public IRuntimeChannelId Id { get; }
        public RuntimeChannelObserver ObserverSource { get; }
        public IRuntimeChannelObserver ObserverReference { get; }
        public IRuntimeChannel Channel { get; }
        public ILogger Logger { get; }
        public IOrleans Orleans { get; }

        public AdaptiveInterval Interval => _failures.Interval;

        public Guid AddHandler(Func<object, Task> handler, Action? onGapDetected)
        {
            var id = _handlers.Add(handler)
                ?? throw new InvalidOperationException($"The last handler of channel {Id.ToRaw()} has gone");

            _gapHandlers.Add(id, onGapDetected);
            return id;
        }

        public bool RemoveHandler(Guid id)
        {
            _gapHandlers.Remove(id);
            return _handlers.Remove(id);
        }

        public Task Resubscribe()
        {
            return _resubscription.Run();
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
                "[Messaging] [Channel] Failed to rebind observer {ObserverId} (attempt {Count}) to channel {ChannelId}",
                ObserverSource.Id, attempt, Id.ToRaw());
        }

        private void LogHandlerFailure(Exception e, Guid handlerId)
        {
            Logger.LogError(e,
                "[Messaging] [Channel] Local handler {HandlerId} failed for channel {ChannelId}",
                handlerId, Id.ToRaw());
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
    }
}
