namespace Game.MatchMetrics;

// IsPlayer: the user has (or had) a seat in the match; an unknown match is IsPlayer false. ClosedAt: the server time the
// match closed, null while it is open. The collector still takes the batches of a closed match during
// MatchMetricsOptions.CloseGrace (the last batch of a run leaves at its end, when the match closes too), then answers Closed.
public readonly record struct MatchMetricsAdmit(bool IsPlayer, DateTime? ClosedAt);
