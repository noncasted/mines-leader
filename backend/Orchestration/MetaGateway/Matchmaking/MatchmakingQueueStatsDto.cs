namespace MetaGateway.Matchmaking;

public record MatchmakingQueueStatsDto
{
    public required string Type { get; init; }
    public required int Count { get; init; }
    public double? OldestWaitSeconds { get; init; }
}
