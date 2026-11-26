namespace Game.MatchMetrics;

// The point where a game says who may write into a match: the collector itself knows nothing of matches. Called on the Open
// of a client stream (not on every batch) and again by a connection whose answer is older than
// MatchMetricsOptions.AdmissionRecheck. A failure of its reads throws: the request fails, the client sends again later.
public interface IMatchMetricsAdmission
{
    Task<MatchMetricsAdmit> Check(Guid matchId, Guid userId);
}
