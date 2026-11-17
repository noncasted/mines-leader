using Meta.Users;
using Shared;

namespace Meta.Matches;

// The end of a queue search (SharedMatchmaking.SearchMatch), sent as a one-time projection to the searching user.
// Command results (Create, CreateWithBot, SearchLobby) are responses and do not use it.
[GenerateSerializer]
public class MatchSearchResultUpdate : IProjectionPayload
{
    // N10: SharedMatchmaking.SearchMatch.RequestId of the search, the client drops a result of another search.
    [Id(0)] public required Guid RequestId { get; init; }
    [Id(1)] public required SharedMatchmaking.MatchSearchStatus Status { get; init; }
    [Id(2)] public required GameMatchType Type { get; init; }

    // Set only when Status is Matched.
    [Id(3)] public Guid SessionId { get; init; }
    [Id(4)] public string? ServerUrl { get; init; }

    // S4: the entry of this user into the session (SessionTickets), one per participant.
    [Id(5)] public string? Ticket { get; init; }

    public static MatchSearchResultUpdate Matched(CreatedMatch match, Guid userId, Guid requestId)
    {
        return new MatchSearchResultUpdate
        {
            RequestId = requestId,
            Status = SharedMatchmaking.MatchSearchStatus.Matched,
            Type = match.Type,
            SessionId = match.SessionId,
            ServerUrl = match.ServerUrl,
            Ticket = match.Tickets[userId]
        };
    }

    public static MatchSearchResultUpdate Failed(GameMatchType type, Guid requestId)
    {
        return new MatchSearchResultUpdate
        {
            RequestId = requestId,
            Status = SharedMatchmaking.MatchSearchStatus.Failed,
            Type = type
        };
    }

    public INetworkContext ToContext()
    {
        return new SharedMatchmaking.MatchResult
        {
            RequestId = RequestId,
            Status = Status,
            Type = Type,
            SessionId = SessionId,
            ServerUrl = ServerUrl,
            Ticket = Ticket
        };
    }
}
