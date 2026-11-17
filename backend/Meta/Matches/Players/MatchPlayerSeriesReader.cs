using Game.MatchMetrics;
using static Meta.Matches.MatchGameMetrics;

namespace Meta.Matches;

// The series of one player of a match from the collector of the game metrics (MatchPlayerSeries).
public sealed class MatchPlayerSeriesReader
{
    public MatchPlayerSeriesReader(IMatchMetricsReader reader)
    {
        _reader = reader;
    }

    private readonly IMatchMetricsReader _reader;

    public async Task<MatchPlayerSeries> Get(Guid matchId, MatchState match, Guid userId, MatchMetricStep step, MatchMetricAxis axis)
    {
        if (match.IsPlayer(userId) == false)
            throw new ArgumentException($"[Match] User {userId} is not a participant of match {matchId}", nameof(userId));

        var lines = new List<MatchPlayerLine>();

        lines.AddRange(await Tagged(matchId, userId, CellsOpened, SourceTag, step, axis));

        foreach (var name in PlainCounters)
            lines.AddRange(await Tagged(matchId, userId, name, null, step, axis));

        lines.AddRange(await Tagged(matchId, userId, CardsPlayed, CardTag, step, axis));

        return new MatchPlayerSeries
        {
            UserId = userId,
            Step = (int)step,
            Axis = axis,
            Lines = lines
        };
    }

    // A line per value of the tag, by the value; without a tag - one line, or none when the player has no points.
    private async Task<IEnumerable<MatchPlayerLine>> Tagged(
        Guid matchId,
        Guid userId,
        string name,
        string? tag,
        MatchMetricStep step,
        MatchMetricAxis axis)
    {
        var groups = await _reader.GetPoints(matchId, new MatchMetricPointsQuery
        {
            Filter = new MatchMetricFilter
            {
                Name = name,
                UserId = userId,
                GroupBy = tag == null ? Array.Empty<string>() : [tag]
            },
            Step = step,
            Axis = axis
        });

        if (tag == null && groups.Count > 1)
            throw new InvalidOperationException($"[Match] {groups.Count} groups of {name} without a group key");

        return groups.Select(group => new MatchPlayerLine(tag == null ? name : $"{name}:{group.Group[tag]}", group.Points))
                     .OrderBy(line => line.Key, StringComparer.Ordinal);
    }
}
