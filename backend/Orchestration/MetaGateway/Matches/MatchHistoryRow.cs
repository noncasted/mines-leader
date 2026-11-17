using Meta.Matches;
using Shared;

namespace MetaGateway.Matches;

// A match of the lists and the card of one match (CONTRACT.md): the record of the match as it is.
public sealed record MatchHistoryRow
{
    public required Guid Id { get; init; }
    public required GameMatchType Type { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime? ClosedAt { get; init; }
    public required double? DurationSeconds { get; init; }
    public required Guid? Winner { get; init; }
    public required IReadOnlyList<MatchHistorySeat> Players { get; init; }

    public static MatchHistoryRow From(Guid id, MatchState match)
    {
        return new MatchHistoryRow
        {
            Id = id,
            Type = match.Type,
            StartedAt = match.StartDate,
            ClosedAt = match.ClosedAt,
            DurationSeconds = match.IsClosed == true ? match.Time.TotalSeconds : null,
            Winner = match.HasWinner == true ? match.Winner : null,
            Players = match.Participants.Select(userId => MatchHistorySeat.From(match, userId)).ToArray()
        };
    }
}
