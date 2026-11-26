namespace MetaGateway.Matchmaking;

public record MatchmakingStatsDto
{
    public int TotalInQueue { get; init; }
    public IReadOnlyList<MatchmakingQueueStatsDto> Queues { get; init; } = [];
}
