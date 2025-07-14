using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessagingObserversCollection
{
    public MessagingObserversCollection(ILogger logger, TimeSpan timeout)
    {
        _logger = logger;
        _timeout = timeout;
    }

    private readonly ILogger _logger;
    private readonly TimeSpan _timeout;
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public void Add(MessagingObserverOverview overview, IMessagingObserver observer)
    {
        _entries[overview.ServiceId] = new Entry
        {
            Observer = observer,
            LastTimeSeen = DateTime.UtcNow,
            Overview = overview
        };
    }

    public async Task Notify(Func<MessagingObserverOverview, bool> selector, Func<IMessagingObserver, Task> action)
    {
        var targets = _entries
            .Where(x => selector(x.Value.Overview))
            .Select(x => x.Value)
            .ToList();

        if (targets.Count == 0)
        {
            _logger.LogWarning("[Messaging] Notify failed: No entries found");
            return;
        }

        var failed = new List<Guid>();

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

                    failed.Add(target.Overview.ServiceId);
                }
            }
        }
        
        foreach (var failedID in failed)
            _entries.Remove(failedID, out _);
        
        targets.RemoveAll(x => failed.Contains(x.Overview.ServiceId));

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