using Internal;

namespace Game.MatchMetrics;

// What the commands of the meta socket of a game call: the commands stay in the game, the id of the user comes from its
// session, never from the client.
public interface IMatchMetricsConnections
{
    // The streams of the socket with this lifetime, created by its first message, forgotten at its end.
    MatchMetricsConnection Of(IReadOnlyLifetime connection, Guid userId);
}
