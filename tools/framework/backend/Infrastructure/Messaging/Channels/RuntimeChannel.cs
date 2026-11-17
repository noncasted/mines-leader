using System.Collections.Concurrent;
using System.Threading.Channels;
using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimeChannel : IGrainWithStringKey
{
    Task<ChannelPosition> AddObserver(Guid id, IRuntimeChannelObserver observer);
    Task RemoveObserver(Guid id);

    // Publishes messages in Orleans grain turn order. Concurrent callers should not infer ordering from caller-side
    // task creation order.
    Task Publish(object message);

    Task<CatchUpResult> CatchUp(long lastSeenSequence);
}

// Delivery semantics (see .claude/docs/MESSAGING.md):
// - every observer has its own ordered delivery queue, so a slow observer delays only itself;
// - an observer whose Send fails, exceeds DeliveryTimeoutSeconds or overflows its queue is removed
//   and gets the missed messages through CatchUp when its client resubscribes (within CatchUpBufferSize);
// - there is no ordering between different observers.
public class RuntimeChannel : Grain, IRuntimeChannel
{
    public RuntimeChannel(ILogger<RuntimeChannel> logger, IRuntimeChannelConfig config)
    {
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<RuntimeChannel> _logger;
    private readonly IRuntimeChannelConfig _config;
    private readonly ConcurrentDictionary<Guid, ObserverData> _observers = new();

    private RuntimeChannelCatchUpBuffer _catchUpBuffer = null!;
    private RuntimeChannelDelivery _delivery = null!;
    private int _bufferSize;
    private DateTime _keepAliveUntil;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _bufferSize = _config.Value.CatchUpBufferSize;
        var channelName = this.GetPrimaryKeyString();
        _catchUpBuffer = new RuntimeChannelCatchUpBuffer(_bufferSize, channelName, _logger);
        _delivery = new RuntimeChannelDelivery(channelName, _config, _logger, Remove);
        return Task.CompletedTask;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        // Deactivation can not be postponed from here (DelayDeactivation only affects idle collection
        // before it starts), so finish what is already queued. Observers re-attach to the next activation
        // through their resubscribe loop.
        var observers = _observers.Values.ToArray();

        foreach (var data in observers)
            data.Queue.Writer.TryComplete();

        var deliveries = Task.WhenAll(observers.Select(data => data.DeliveryTask));
        await Task.WhenAny(deliveries, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    public Task<ChannelPosition> AddObserver(Guid id, IRuntimeChannelObserver observer)
    {
        if (_observers.TryGetValue(id, out var data) == false)
        {
            var queue = Channel.CreateBounded<SequencedMessage>(new BoundedChannelOptions(_bufferSize)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            data = new ObserverData(id, observer, queue);

            _observers[id] = data;

            data.SetDelivery(Task.Factory.StartNew(() => _delivery.Run(data), CancellationToken.None,
                TaskCreationOptions.None, TaskScheduler.Current).Unwrap());
        }

        data.Refresh(observer);

        // Observers refresh themselves periodically (resubscribe loop); keep the activation and its
        // catch-up buffer alive between refreshes.
        KeepAlive(TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes));

        return Task.FromResult(_catchUpBuffer.Position);
    }

    public Task RemoveObserver(Guid id)
    {
        if (_observers.TryRemove(id, out var data))
            data.Queue.Writer.TryComplete();

        return Task.CompletedTask;
    }

    public Task Publish(object message)
    {
        var channelName = this.GetPrimaryKeyString();
        using var activity = RuntimeChannelTelemetry.Source.StartActivity("RuntimeChannel.Publish");
        activity?.SetTag("messaging.channel", channelName);
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("observer.count", _observers.Count);

        RuntimeChannelTelemetry.Published.Add(1);
        RuntimeChannelTelemetry.ObserverCount.Record(_observers.Count);

        var sequenced = _catchUpBuffer.Add(message);
        activity?.SetTag("messaging.sequence", sequenced.Sequence);

        foreach (var data in _observers.Values)
        {
            if (data.Queue.Writer.TryWrite(sequenced) == true)
                continue;

            // Behind by a whole catch-up buffer: drop it, the client recovers through CatchUp / gap detection.
            RuntimeChannelTelemetry.DeliveryFailure.Add(1);

            _logger.LogWarning(
                "[Messaging] [Channel] Delivery queue overflow on {ChannelName} for observer {ObserverId} at sequence {Sequence}",
                channelName, data.Id, sequenced.Sequence);

            Remove(data);
        }

        // Keep the grain alive while delivery is in progress.
        KeepAlive(TimeSpan.FromSeconds(_config.Value.DeliveryTimeoutSeconds));

        return Task.CompletedTask;
    }

    public Task<CatchUpResult> CatchUp(long lastSeenSequence)
    {
        return Task.FromResult(_catchUpBuffer.CatchUp(lastSeenSequence));
    }

    // DelayDeactivation replaces the previous request: never shorten a longer keep-alive.
    private void KeepAlive(TimeSpan duration)
    {
        var until = DateTime.UtcNow + duration;

        if (until <= _keepAliveUntil)
            return;

        _keepAliveUntil = until;
        DelayDeactivation(duration);
    }

    private void Remove(ObserverData data)
    {
        _observers.TryRemove(KeyValuePair.Create(data.Id, data));
        data.Queue.Writer.TryComplete();
    }

    public class ObserverData
    {
        public ObserverData(Guid id, IRuntimeChannelObserver observer, Channel<SequencedMessage> queue)
        {
            Id = id;
            Observer = observer;
            Queue = queue;
        }

        public Guid Id { get; }
        public IRuntimeChannelObserver Observer { get; private set; }
        public Channel<SequencedMessage> Queue { get; }
        public Task DeliveryTask { get; private set; } = Task.CompletedTask;

        public void Refresh(IRuntimeChannelObserver observer)
        {
            Observer = observer;
        }

        public void SetDelivery(Task task)
        {
            DeliveryTask = task;
        }
    }
}
