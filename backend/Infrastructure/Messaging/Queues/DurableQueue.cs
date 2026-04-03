using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDurableQueue : IGrainWithStringKey
{
    Task AddObserver(Guid id, IDurableQueueObserver observer);
    Task Push(object message);
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
        if (_observers.Count == 0)
            return Task.CompletedTask;

        var latestUpdate = _observers.Values.Max(t => t.UpdateDate);
        var timeSinceLastUpdate = DateTime.UtcNow - latestUpdate;
        var keepAlive = TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes);

        if (timeSinceLastUpdate < keepAlive)
            DelayDeactivation(keepAlive - timeSinceLastUpdate);

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

    public async Task Push(object message)
    {
        BackendMetrics.DurableQueuePushed.Add(1);
        BackendMetrics.DurableQueueObserverCount.Record(_observers.Count);

        var toRemove = new List<Guid>();

        await Task.WhenAll(_observers.Values.Select(data => SendSafe(data)));

        foreach (var id in toRemove)
            _observers.Remove(id);

        async Task SendSafe(ObserverData data)
        {
            try
            {
                await data.Observer.Send(new List<object>() { message });
            }
            catch (Exception e)
            {
                toRemove.Add(data.Id);
                BackendMetrics.DurableQueueDeliveryFailure.Add(1);

                _logger.LogError(e,
                    "[Messaging] [DurableQueue] Delivering message from {QueueName} to observer failed",
                    this.GetPrimaryKeyString()
                );
            }
        }
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IDurableQueueObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}