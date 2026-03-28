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

        if (timeSinceLastUpdate > TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes))
            return Task.CompletedTask;

        throw new Exception("[Messaging] [DurableQueue] Keeping queue alive because observer was recently set");
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
        var toRemove = new List<Guid>();

        await Task.WhenAll(_observers.Values.Select(data =>
                {
                    var observer = data.Observer;

                    try
                    {
                        return observer.Send(new List<object>() { message });
                    }
                    catch (Exception e)
                    {
                        toRemove.Add(data.Id);

                        _logger.LogError(e,
                            "[Messaging] [DurableQueue] Delivering message from {QueueName} to observer failed",
                            this.GetPrimaryKeyString()
                        );

                        return Task.CompletedTask;
                    }
                }
            )
        );

        foreach (var observer in toRemove)
            _observers.Remove(observer);
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IDurableQueueObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}