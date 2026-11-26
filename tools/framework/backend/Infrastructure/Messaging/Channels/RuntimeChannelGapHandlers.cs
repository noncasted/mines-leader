using Microsoft.Extensions.Logging;

namespace Infrastructure;

// The callbacks of the local handlers that reload their state after a gap: one that throws does not stop the others.
internal class RuntimeChannelGapHandlers
{
    public RuntimeChannelGapHandlers(IRuntimeChannelId id, ILogger logger)
    {
        _id = id;
        _logger = logger;
    }

    private readonly IRuntimeChannelId _id;
    private readonly ILogger _logger;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Action> _handlers = new();

    public void Add(Guid handlerId, Action? onGapDetected)
    {
        if (onGapDetected == null)
            return;

        lock (_gate)
            _handlers[handlerId] = onGapDetected;
    }

    public void Remove(Guid handlerId)
    {
        lock (_gate)
            _handlers.Remove(handlerId);
    }

    public void Invoke()
    {
        Action[] handlers;

        lock (_gate)
            handlers = _handlers.Values.ToArray();

        foreach (var handler in handlers)
        {
            try
            {
                handler();
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "[Messaging] [Channel] Gap handler failed for channel {ChannelId}",
                    _id.ToRaw());
            }
        }
    }
}
