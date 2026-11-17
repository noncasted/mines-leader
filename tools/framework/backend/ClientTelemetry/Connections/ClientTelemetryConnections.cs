using Common.Extensions;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;

namespace Game.ClientTelemetry;

internal sealed class ClientTelemetryConnections : IClientTelemetryConnections
{
    public ClientTelemetryConnections(IClientTelemetrySwitch telemetrySwitch, IOrleans orleans, ILogger<ClientTelemetryConnection> logger)
    {
        _switch = telemetrySwitch;
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IClientTelemetrySwitch _switch;
    private readonly IOrleans _orleans;
    private readonly ILogger<ClientTelemetryConnection> _logger;
    private readonly Dictionary<IReadOnlyLifetime, ClientTelemetryConnection> _connections = new();

    public ClientTelemetryConnection Of(IReadOnlyLifetime connection)
    {
        lock (_connections)
        {
            if (_connections.TryGetValue(connection, out var known))
                return known;

            var created = new ClientTelemetryConnection(_switch, _orleans, _logger);
            _connections.Add(connection, created);

            // Runs at once on a lifetime already over: a message late for its socket gets a connection that starts nothing.
            connection.Listen(() => {
                lock (_connections)
                    _connections.Remove(connection);

                created.OnDisconnected().NoAwait();
            });

            return created;
        }
    }
}
