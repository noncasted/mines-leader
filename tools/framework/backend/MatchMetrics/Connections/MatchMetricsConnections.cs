using Internal;
using Microsoft.Extensions.Logging;

namespace Game.MatchMetrics;

internal sealed class MatchMetricsConnections : IMatchMetricsConnections
{
    public MatchMetricsConnections(
        IMatchMetricsAdmission admission,
        MatchMetricStreamStore streams,
        MatchMetricBatchWriter writer,
        MatchMetricsOptions options,
        TimeProvider time,
        ILogger<MatchMetricsConnection> logger)
    {
        _admission = admission;
        _streams = streams;
        _writer = writer;
        _options = options;
        _time = time;
        _logger = logger;
    }

    private readonly IMatchMetricsAdmission _admission;
    private readonly MatchMetricStreamStore _streams;
    private readonly MatchMetricBatchWriter _writer;
    private readonly MatchMetricsOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MatchMetricsConnection> _logger;
    private readonly Dictionary<IReadOnlyLifetime, MatchMetricsConnection> _connections = new();

    public MatchMetricsConnection Of(IReadOnlyLifetime connection, Guid userId)
    {
        lock (_connections)
        {
            if (_connections.TryGetValue(connection, out var known))
                return known;

            var created = new MatchMetricsConnection(userId, _admission, _streams, _writer, _options, _time, _logger);
            _connections.Add(connection, created);

            // Runs at once on a lifetime already over: a late message gets a connection that is forgotten at once.
            connection.Listen(() => {
                lock (_connections)
                    _connections.Remove(connection);
            });

            return created;
        }
    }
}
