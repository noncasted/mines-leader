using Game.MatchMetrics;
using Infrastructure;

namespace Meta.Matches;

// Who may write the metrics of a match (the collector of Game.MatchMetrics): a participant of the record of the match
// (IMatch, the key is the id of the game session), until the match closes - with a winner or without one. A match never
// set up has the empty record: no participants, not closed.
public class MatchMetricsAdmission : IMatchMetricsAdmission
{
    public MatchMetricsAdmission(IOrleans orleans)
    {
        _orleans = orleans;
    }

    private readonly IOrleans _orleans;

    public async Task<MatchMetricsAdmit> Check(Guid matchId, Guid userId)
    {
        var match = _orleans.GetGrain<IMatch>(matchId);
        var state = await _orleans.Transactions.Run(match.GetState);

        return new MatchMetricsAdmit(state.IsPlayer(userId), state.ClosedAt);
    }
}
