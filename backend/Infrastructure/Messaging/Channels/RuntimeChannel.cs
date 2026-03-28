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
    public RuntimeChannel(ILogger<RuntimeChannel> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<RuntimeChannel> _logger;
    private readonly ConcurrentDictionary<Guid, ObserverData> _observers = new();

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
                            "[Messaging] [Channel] Delivering message from {ChannelName} to observer failed",
                            this.GetPrimaryKeyString()
                        );

                        return Task.CompletedTask;
                    }
                }
            )
        );

        foreach (var observer in toRemove)
            _observers.TryRemove(observer, out _);
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IRuntimeChannelObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}