using System.Text.Json;
using Game.MatchMetrics;

namespace Meta.Matches;

// An event of a player as it was written: turn.end {number, seconds, cells, cards}, card.play {card, board, cells},
// match.end {result, seconds}. Time - seconds of the match clock, At - the server time.
public sealed record MatchPlayerMoment(string Name, float Time, DateTime At, JsonElement Fields)
{
    public static MatchPlayerMoment From(MatchMetricEventInfo info)
    {
        return new MatchPlayerMoment(info.Name, info.Time, info.At, info.Fields);
    }
}
