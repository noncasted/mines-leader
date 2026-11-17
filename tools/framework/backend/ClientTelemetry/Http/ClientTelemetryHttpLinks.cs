using Microsoft.Extensions.Logging;

namespace Game.ClientTelemetry;

// The virtual sockets of the HTTP intake: the link id a client makes on the load of its page is its connection, the same rules as a
// meta socket follow (IClientTelemetryConnections.Of(lifetime)). Every request keeps the link alive; a silence of IdleAfter ends its
// lifetime, which closes its session as lost, as the close of a socket does. A request with an unknown id opens a new link: the
// session and the startup ids of an ended one are not there, so its batches get enabled false.
internal sealed class ClientTelemetryHttpLinks
{
    public ClientTelemetryHttpLinks(
        IClientTelemetryConnections connections,
        ClientTelemetryHttpOptions options,
        ClientTelemetryStorageOptions storage,
        TimeProvider time,
        ILogger<ClientTelemetryHttpLinks> logger)
    {
        if (options.IdleAfter <= TimeSpan.Zero || options.IdleAfter >= storage.LostAfter)
            throw new ArgumentOutOfRangeException(nameof(options), options.IdleAfter, "[ClientTelemetry] IdleAfter must be above zero and below LostAfter");

        _connections = connections;
        _idleAfter = options.IdleAfter;
        _time = time;
        _logger = logger;
    }

    private readonly IClientTelemetryConnections _connections;
    private readonly TimeSpan _idleAfter;
    private readonly TimeProvider _time;
    private readonly ILogger<ClientTelemetryHttpLinks> _logger;
    private readonly Dictionary<Guid, ClientTelemetryHttpLink> _links = new();

    public ClientTelemetryConnection Of(Guid linkId)
    {
        lock (_links)
        {
            if (!_links.TryGetValue(linkId, out var link))
            {
                link = new ClientTelemetryHttpLink(linkId, _time, Expire);
                _links.Add(linkId, link);
            }

            link.Touch(_time.GetUtcNow(), _idleAfter);

            return _connections.Of(link.Lifetime);
        }
    }

    // A request that came while the timer fired has moved the time on and armed the timer again.
    private void Expire(ClientTelemetryHttpLink link)
    {
        lock (_links)
        {
            if (_time.GetUtcNow() - link.LastRequest < _idleAfter)
                return;

            _links.Remove(link.Id);
        }

        try
        {
            link.End();
        }
        catch (Exception exception)
        {
            // The boundary of a timer nobody awaits: the listeners are the closes of the sessions, a failure of them is logged there too.
            _logger.LogWarning(exception, "[ClientTelemetry] The end of an HTTP link failed");
        }
    }
}
