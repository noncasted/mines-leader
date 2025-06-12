using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessagingObserversCollection
{
    public MessagingObserversCollection(ILogger logger, TimeSpan timeout)
    {
        _logger = logger;
        _timeout = timeout;
    }

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger;
    private readonly TimeSpan _timeout;
    private readonly Dictionary<Guid, Entry> _entries = new();

    public void Add(MessagingObserverOverview overview, IMessagingObserver observer)
    {
        _lock.Wait();
        
        _entries[overview.ServiceId] = new Entry
        {
            Observer = observer,
            LastTimeSeen = DateTime.UtcNow,
            Overview = overview
        };
        
        _lock.Release();
    }

    public async Task Notify(Func<MessagingObserverOverview, bool> selector, Func<IMessagingObserver, Task> action)
    {
        await _lock.WaitAsync();

        var targets = _entries
            .Where(x => selector(x.Value.Overview))
            .Select(x => x.Value)
            .ToList();

        _lock.Release();
        
        if (targets.Count == 0)
        {
            _logger.LogWarning("[Messaging] Notify failed: No entries found");
            return;
        }

        foreach (var target in targets)
        {
            var timeDifference = DateTime.UtcNow - target.LastTimeSeen;

            if (timeDifference > _timeout)
            {
                _logger.LogWarning("[Messaging] Notify failed: Entry timeout: {entryID}", target.Overview.ServiceId);

                try
                {
                    await target.Observer.Ping();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Messaging] Notify failed: Entry is outdated and ping failed: {entryID}",
                        targets);
            
                    return;
                }
            }
        }

        foreach (var target in targets)
        {
            try
            {
                await action(target.Observer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Messaging] Notify failed: {entryID}", targets);
            }
        }
    }

    public class Entry
    {
        public required IMessagingObserver Observer { get; init; }
        public required DateTime LastTimeSeen { get; init; }
        public required MessagingObserverOverview Overview { get; init; }
    }
}