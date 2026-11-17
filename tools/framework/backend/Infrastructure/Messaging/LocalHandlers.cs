namespace Infrastructure;

// The handlers of one observer of a process, called one by one: the delivery succeeds if at least one of them
// succeeded, a failed one is logged and does not get the message again (.claude/docs/MESSAGING.md).
internal class LocalHandlers
{
    public LocalHandlers(string transport, string target, Action<Exception, Guid> logFailure)
    {
        _transport = transport;
        _target = target;
        _logFailure = logFailure;
    }

    private readonly string _transport;
    private readonly string _target;
    private readonly Action<Exception, Guid> _logFailure;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Func<object, Task>> _handlers = new();

    private bool _isRetired;

    // Null once the last handler has gone: the observer is on its way out and a new handler would get nothing.
    public Guid? Add(Func<object, Task> handler)
    {
        var id = Guid.NewGuid();

        lock (_gate)
        {
            if (_isRetired == true)
                return null;

            _handlers[id] = handler;
        }

        return id;
    }

    // True when no handler is left.
    public bool Remove(Guid id)
    {
        lock (_gate)
        {
            _handlers.Remove(id);
            _isRetired = _handlers.Count == 0;
            return _isRetired;
        }
    }

    public async Task Deliver(object message)
    {
        KeyValuePair<Guid, Func<object, Task>>[] handlers;

        lock (_gate)
            handlers = _handlers.ToArray();

        if (handlers.Length == 0)
            throw new InvalidOperationException($"No local {_transport} handlers for {_target}");

        var successCount = 0;
        List<Exception>? failures = null;

        foreach (var (handlerId, handler) in handlers)
        {
            try
            {
                await handler(message);
                successCount++;
            }
            catch (Exception e)
            {
                failures ??= [];
                failures.Add(e);

                _logFailure(e, handlerId);
            }
        }

        if (successCount == 0 && failures != null)
            throw new AggregateException($"All local {_transport} handlers failed for {_target}", failures);
    }
}
