using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDurableQueue : IGrainWithStringKey
{
    Task AddObserver(Guid id, IDurableQueueObserver observer);
    Task RemoveObserver(Guid id);
    Task Push(object message, Guid correlationId = default);
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

    private readonly Dictionary<Guid, ObserverData> _observers = new();

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        var delay = MessagingGrainExtensions.GetKeepAliveDelay(_observers.Values, d => d.UpdateDate,
            _config.Value.ObserverKeepAliveMinutes);

        if (delay != null)
            DelayDeactivation(delay.Value);
        return Task.CompletedTask;
    }

    public Task AddObserver(Guid id, IDurableQueueObserver observer)
    {
        if (_observers.TryGetValue(id, out var data) == false)
        {
            data = new ObserverData
            {
                Observer = observer,
                UpdateDate = DateTime.UtcNow,
                Id = id
            };

            _observers[id] = data;
        }

        data.Observer = observer;
        data.UpdateDate = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    public Task RemoveObserver(Guid id)
    {
        _observers.Remove(id);
        return Task.CompletedTask;
    }

    public async Task Push(object message, Guid correlationId = default)
    {
        var queueName = this.GetPrimaryKeyString();
        using var activity = TraceExtensions.MessagingDurableQueue.StartActivity("DurableQueue.Push");
        activity?.SetTag("messaging.queue", queueName);
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("observer.count", _observers.Count);

        if (correlationId != Guid.Empty)
            activity?.SetTag("messaging.correlation_id", correlationId);

        BackendMetrics.DurableQueuePushed.Add(1);
        BackendMetrics.DurableQueueObserverCount.Record(_observers.Count);

        if (_observers.Count == 0)
        {
            BackendMetrics.DurableQueueNoSubscribers.Add(1);

            _logger.LogWarning(
                "[Messaging] [DurableQueue] No active subscribers for queue '{QueueName}' (correlation {CorrelationId})",
                queueName, correlationId);

            throw new InvalidOperationException(
                $"No active subscribers for durable queue '{queueName}'. Message left in processing for requeue.");
        }

        var toRemove = new List<Guid>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var data in _observers.Values.ToArray())
        {
            try
            {
                await data.Observer.Send(message);
                successCount++;
            }
            catch (Exception e)
            {
                failedCount++;
                toRemove.Add(data.Id);
                BackendMetrics.DurableQueueDeliveryFailure.Add(1);

                _logger.LogError(e,
                    "[Messaging] [DurableQueue] Delivering message from {QueueName} to observer {ObserverId} failed (correlation {CorrelationId})",
                    queueName, data.Id, correlationId);
            }
        }

        foreach (var id in toRemove)
            _observers.Remove(id);

        activity?.SetTag("observer.success_count", successCount);
        activity?.SetTag("observer.failure_count", failedCount);

        if (successCount > 0)
        {
            if (failedCount > 0)
            {
                _logger.LogWarning(
                    "[Messaging] [DurableQueue] Delivered message from {QueueName} to {SuccessCount}/{ObserverCount} observers; removed {FailedCount} failed observers (correlation {CorrelationId})",
                    queueName, successCount, successCount + failedCount, failedCount, correlationId);
            }

            return;
        }

        BackendMetrics.DurableQueueNoSubscribers.Add(1);

        _logger.LogWarning(
            "[Messaging] [DurableQueue] No successful deliveries on queue '{QueueName}': observers={ObserverCount}, failed={FailedCount}, remaining={RemainingCount}, correlation={CorrelationId}",
            queueName, successCount + failedCount, failedCount, _observers.Count, correlationId);

        throw new InvalidOperationException(
            $"No subscribers successfully processed durable queue '{queueName}'. Message left in processing for requeue.");
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IDurableQueueObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}
