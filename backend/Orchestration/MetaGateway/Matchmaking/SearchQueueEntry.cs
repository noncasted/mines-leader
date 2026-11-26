namespace MetaGateway.Matchmaking;

public class SearchQueueEntry
{
    public SearchQueueEntry(Guid userId, Guid requestId, DateTime joinedAt)
    {
        UserId = userId;
        RequestId = requestId;
        JoinedAt = joinedAt;
    }

    public Guid UserId { get; }

    // N10: SharedMatchmaking.SearchMatch.RequestId, echoed in the search result.
    public Guid RequestId { get; }

    public DateTime JoinedAt { get; }
}
