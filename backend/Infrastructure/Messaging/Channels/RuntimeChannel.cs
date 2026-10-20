using System.Collections.Concurrent;
using System.Threading.Channels;
using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimeChannelId
{
    string ToRaw();
}

public class RuntimeChannelId : IRuntimeChannelId
{
    public RuntimeChannelId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

[GenerateSerializer]
public class SequencedMessage
{
    [Id(0)] public long Sequence { get; set; }
    [Id(1)] public object Payload { get; set; } = null!;
}

[GenerateSerializer]
public class CatchUpResult
{
    [Id(0)] public IReadOnlyList<SequencedMessage> Messages { get; set; } = Array.Empty<SequencedMessage>();
    [Id(1)] public bool GapDetected { get; set; }
    [Id(2)] public long CurrentSequence { get; set; }
}

public interface IRuntimeChannel : IGrainWithStringKey
{
    Task AddObserver(Guid id, IRuntimeChannelObserver observer);
    Task RemoveObserver(Guid id);

    /// <summary>
    /// Publishes messages in Orleans grain turn order. Concurrent callers should not infer ordering from caller-side task creation order.
    /// </summary>
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

    private long _sequenceNumber;
    private SequencedMessage?[] _buffer = null!;
    private int _bufferSize;
    private DateTime _keepAliveUntil;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _bufferSize = _config.Value.CatchUpBufferSize;
        _buffer = new SequencedMessage?[_bufferSize];
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

    public Task AddObserver(Guid id, IRuntimeChannelObserver observer)
    {
        if (_observers.TryGetValue(id, out var data) == false)
        {
            data = new ObserverData
            {
                Observer = observer,
                UpdateDate = DateTime.UtcNow,
                Id = id,
                Queue = Channel.CreateBounded<SequencedMessage>(new BoundedChannelOptions(_bufferSize)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                })
            };

            _observers[id] = data;

            data.DeliveryTask = Task.Factory.StartNew(() => DeliverTo(data), CancellationToken.None,
                TaskCreationOptions.None, TaskScheduler.Current).Unwrap();
        }

        data.Observer = observer;
        data.UpdateDate = DateTime.UtcNow;

        // Observers refresh themselves periodically (resubscribe loop); keep the activation and its
        // catch-up buffer alive between refreshes.
        KeepAlive(TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes));

        return Task.CompletedTask;
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
        using var activity = TraceExtensions.MessagingRuntimeChannel.StartActivity("RuntimeChannel.Publish");
        activity?.SetTag("messaging.channel", channelName);
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("observer.count", _observers.Count);

        BackendMetrics.ChannelPublished.Add(1);
        BackendMetrics.ChannelObserverCount.Record(_observers.Count);

        _sequenceNumber++;
        var sequenced = new SequencedMessage { Sequence = _sequenceNumber, Payload = message };
        _buffer[_sequenceNumber % _bufferSize] = sequenced;
        activity?.SetTag("messaging.sequence", sequenced.Sequence);

        foreach (var data in _observers.Values)
        {
            if (data.Queue.Writer.TryWrite(sequenced) == true)
                continue;

            // Behind by a whole catch-up buffer: drop it, the client recovers through CatchUp / gap detection.
            BackendMetrics.ChannelDeliveryFailure.Add(1);

            _logger.LogWarning(
                "[Messaging] [Channel] Delivery queue overflow on {ChannelName} for observer {ObserverId} at sequence {Sequence}",
                channelName, data.Id, sequenced.Sequence);

            Remove(data);
        }

        // Keep the grain alive while delivery is in progress.
        KeepAlive(TimeSpan.FromSeconds(_config.Value.DeliveryTimeoutSeconds));

        return Task.CompletedTask;
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

    private async Task DeliverTo(ObserverData data)
    {
        await foreach (var sequenced in data.Queue.Reader.ReadAllAsync())
        {
            if (await TrySend(data, sequenced) == true)
                continue;

            Remove(data);
            return;
        }
    }

    private async Task<bool> TrySend(ObserverData data, SequencedMessage sequenced)
    {
        var channelName = this.GetPrimaryKeyString();

        try
        {
            var timeout = TimeSpan.FromSeconds(_config.Value.DeliveryTimeoutSeconds);
            var deliveryTask = data.Observer.Send(sequenced);

            if (await Task.WhenAny(deliveryTask, Task.Delay(timeout)) != deliveryTask)
            {
                BackendMetrics.ChannelDeliveryTimeout.Add(1);

                _logger.LogWarning(
                    "[Messaging] [Channel] Delivery timeout on {ChannelName} to observer {ObserverId} at sequence {Sequence}",
                    channelName, data.Id, sequenced.Sequence);
                return false;
            }

            await deliveryTask;
            return true;
        }
        catch (Exception e)
        {
            BackendMetrics.ChannelDeliveryFailure.Add(1);

            _logger.LogError(e,
                "[Messaging] [Channel] Delivering message from {ChannelName} to observer {ObserverId} failed at sequence {Sequence}",
                channelName, data.Id, sequenced.Sequence);
            return false;
        }
    }

    public Task<CatchUpResult> CatchUp(long lastSeenSequence)
    {
        var channelName = this.GetPrimaryKeyString();

        if (lastSeenSequence > _sequenceNumber)
        {
            BackendMetrics.ChannelGapDetected.Add(1);

            _logger.LogWarning(
                "[Messaging] [Channel] Sequence reset/gap detected on {ChannelName}: requested seq {RequestedSeq}, current seq {CurrentSeq}",
                channelName, lastSeenSequence, _sequenceNumber);

            return Task.FromResult(new CatchUpResult
            {
                GapDetected = true,
                CurrentSequence = _sequenceNumber
            });
        }

        if (lastSeenSequence == _sequenceNumber)
        {
            return Task.FromResult(new CatchUpResult
            {
                CurrentSequence = _sequenceNumber
            });
        }

        var oldestInBuffer = _sequenceNumber - _bufferSize + 1;

        if (oldestInBuffer < 1)
            oldestInBuffer = 1;

        var gapDetected = lastSeenSequence < oldestInBuffer - 1;
        var startSequence = Math.Max(lastSeenSequence + 1, oldestInBuffer);

        var messages = new List<SequencedMessage>();

        for (var seq = startSequence; seq <= _sequenceNumber; seq++)
        {
            var entry = _buffer[seq % _bufferSize];

            if (entry != null && entry.Sequence == seq)
                messages.Add(entry);
        }

        if (messages.Count > 0)
        {
            BackendMetrics.ChannelCatchUpExecuted.Add(1);
            BackendMetrics.ChannelCatchUpMessages.Record(messages.Count);
        }

        if (gapDetected)
        {
            BackendMetrics.ChannelGapDetected.Add(1);

            _logger.LogWarning(
                "[Messaging] [Channel] Gap detected on {ChannelName}: requested seq {RequestedSeq}, oldest available {OldestSeq}, current seq {CurrentSeq}",
                channelName, lastSeenSequence, oldestInBuffer, _sequenceNumber);
        }

        return Task.FromResult(new CatchUpResult
        {
            Messages = messages,
            GapDetected = gapDetected,
            CurrentSequence = _sequenceNumber
        });
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IRuntimeChannelObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
        public required Channel<SequencedMessage> Queue { get; init; }
        public Task DeliveryTask { get; set; } = Task.CompletedTask;
    }
}
