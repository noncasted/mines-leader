using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDurableQueue : IGrainWithStringKey
{
    Task AddObserver(Guid id, IDurableQueueObserver observer);
    Task RemoveObserver(Guid id);
    // Delivers the message to every subscribed observer and fails while one of them has not got it. A retry with the
    // same messageId (DurableQueueSideEffect) goes only to the observers that have not got it yet and keeps failing
    // while an observer that failed has not resubscribed (or removed itself): it gets the message once it is back, the
    // others do not get it again. The ids live in the activation memory, so a reactivation between the retries
    // delivers to everyone again: at least once per observer.
    Task Push(object message, Guid correlationId = default, Guid messageId = default);
}

public class DurableQueue : Grain, IDurableQueue
{
    public DurableQueue(ILogger<DurableQueue> logger, IDurableQueueConfig config)
    {
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<DurableQueue> _logger;
    private readonly IDurableQueueConfig _config;

    private static readonly TimeSpan DeliveredRetention = TimeSpan.FromHours(1);

    private readonly Dictionary<Guid, ObserverData> _observers = new();

    // messageId -> the observers that got a message whose push failed for some other observer and the observers that
    // failed and have not got it yet, until it is retried.
    private readonly Dictionary<Guid, DeliveredData> _delivered = new();

    public Task AddObserver(Guid id, IDurableQueueObserver observer)
    {
        if (_observers.TryGetValue(id, out var data) == false)
        {
            data = new ObserverData(id, observer);
            _observers[id] = data;
        }

        data.Refresh(observer);

        // Observers live only in this activation's memory and refresh themselves periodically (resubscribe loop):
        // keep the activation between refreshes. This must happen here, DelayDeactivation called from
        // OnDeactivateAsync does not stop a deactivation that has already started.
        DelayDeactivation(TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes));

        return Task.CompletedTask;
    }

    public Task RemoveObserver(Guid id)
    {
        _observers.Remove(id);

        // An observer that left on purpose is not waited for by the retries.
        foreach (var data in _delivered.Values)
            data.Failed.Remove(id);

        return Task.CompletedTask;
    }

    public async Task Push(object message, Guid correlationId = default, Guid messageId = default)
    {
        var queueName = this.GetPrimaryKeyString();
        using var activity = DurableQueueTelemetry.Source.StartActivity("DurableQueue.Push");
        activity?.SetTag("messaging.queue", queueName);
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("observer.count", _observers.Count);

        if (correlationId != Guid.Empty)
            activity?.SetTag("messaging.correlation_id", correlationId);

        DurableQueueTelemetry.Pushed.Add(1);
        DurableQueueTelemetry.ObserverCount.Record(_observers.Count);

        if (_observers.Count == 0)
        {
            DurableQueueTelemetry.NoSubscribers.Add(1);

            _logger.LogWarning(
                "[Messaging] [DurableQueue] No active subscribers for queue '{QueueName}' (correlation {CorrelationId})",
                queueName, correlationId);

            throw new InvalidOperationException(
                $"No active subscribers for durable queue '{queueName}'. Message left in processing for requeue.");
        }

        PruneDelivered();

        // A message without an id (a direct push) is not retried by a side effect: nothing to remember.
        var data = messageId != Guid.Empty && _delivered.TryGetValue(messageId, out var known)
            ? known
            : new DeliveredData();

        var targets = _observers.Values.Where(observer => data.Observers.Contains(observer.Id) == false).ToArray();

        // In parallel: an observer on a dead silo holds the grain for one call timeout, not one per dead observer.
        var results = await Task.WhenAll(targets.Select(async observer => {
            try
            {
                await observer.Observer.Send(message);
                return (Observer: observer, Error: (Exception?)null);
            }
            catch (Exception e)
            {
                // Isolation of the observers: the failure is counted and logged below and fails the push.
                return (Observer: observer, Error: e);
            }
        }));

        var failedCount = 0;

        foreach (var (observer, error) in results)
        {
            if (error == null)
            {
                data.Observers.Add(observer.Id);
                data.Failed.Remove(observer.Id);
                continue;
            }

            failedCount++;
            data.Failed.Add(observer.Id);
            _observers.Remove(observer.Id);
            DurableQueueTelemetry.DeliveryFailure.Add(1);

            _logger.LogError(error,
                "[Messaging] [DurableQueue] Delivering message from {QueueName} to observer {ObserverId} failed (correlation {CorrelationId})",
                queueName, observer.Id, correlationId);
        }

        activity?.SetTag("observer.success_count", targets.Length - failedCount);
        activity?.SetTag("observer.failure_count", failedCount);

        // Failed earlier and not resubscribed yet: completing now would lose the message for them.
        var waiting = data.Failed.Count - failedCount;

        if (data.Failed.Count == 0)
        {
            _delivered.Remove(messageId);
            return;
        }

        if (messageId != Guid.Empty)
        {
            data.Keep(DateTime.UtcNow + DeliveredRetention);
            _delivered[messageId] = data;
        }

        _logger.LogWarning(
            "[Messaging] [DurableQueue] Delivered message from {QueueName} to {SuccessCount}/{TargetCount} observers; removed {FailedCount} failed observers, {WaitingCount} failed earlier and have not resubscribed; the retry goes to the rest (correlation {CorrelationId})",
            queueName, targets.Length - failedCount, targets.Length, failedCount, waiting, correlationId);

        throw new InvalidOperationException(
            $"{failedCount} of {targets.Length} observers of durable queue '{queueName}' failed, {waiting} failed earlier and have not resubscribed. Message left in processing for requeue.");
    }

    private void PruneDelivered()
    {
        var now = DateTime.UtcNow;

        foreach (var (id, data) in _delivered.ToArray())
        {
            if (data.Until < now)
                _delivered.Remove(id);
        }
    }

    private class DeliveredData
    {
        public HashSet<Guid> Observers { get; } = new();
        public HashSet<Guid> Failed { get; } = new();
        public DateTime Until { get; private set; }

        public void Keep(DateTime until)
        {
            Until = until;
        }
    }

    public class ObserverData
    {
        public ObserverData(Guid id, IDurableQueueObserver observer)
        {
            Id = id;
            Observer = observer;
        }

        public Guid Id { get; }
        public IDurableQueueObserver Observer { get; private set; }

        public void Refresh(IDurableQueueObserver observer)
        {
            Observer = observer;
        }
    }
}
