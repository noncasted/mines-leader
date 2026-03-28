using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

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

public interface IRuntimeChannel : IGrainWithStringKey
{
    Task AddObserver(Guid id, IRuntimeChannelObserver observer);

    [AlwaysInterleave]
    Task Publish(object message);
}

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

    public Task AddObserver(Guid id, IRuntimeChannelObserver observer)
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

    public async Task Publish(object message)
    {
        var toRemove = new List<Guid>();

        await Task.WhenAll(_observers.Values.Select(data => SendSafe(data)));

        foreach (var id in toRemove)
            _observers.TryRemove(id, out _);

        return;
        
        async Task SendSafe(ObserverData data)
        {
            try
            {
                await data.Observer.Send(new List<object>() { message });
            }
            catch (Exception e)
            {
                toRemove.Add(data.Id);

                _logger.LogError(e,
                    "[Messaging] [Channel] Delivering message from {ChannelName} to observer failed",
                    this.GetPrimaryKeyString()
                );
            }
        }
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IRuntimeChannelObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}