using Internal;

namespace Game.MatchMetrics;

// The stream of the server in a match (kind Server, user Guid.Empty): what only the server sees. Its clock is the server
// time since the open, in seconds; written every MatchMetricsOptions.ServerFlush and at the end of the lifetime.
public interface IMatchMetricsWriter
{
    IMatchMetricsServerStream Open(IReadOnlyLifetime lifetime, Guid matchId);
}
