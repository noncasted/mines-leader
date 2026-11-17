namespace MetaGateway.Matches;

// The matches of one user, newest first; Total - all its matches, also those over the limit.
public sealed record MatchHistoryPlayer
{
    public required Guid UserId { get; init; }

    // The name now; the rows keep the name at the setup.
    public required string Nickname { get; init; }

    public required int Total { get; init; }
    public required IReadOnlyList<MatchHistoryRow> Matches { get; init; }
}
