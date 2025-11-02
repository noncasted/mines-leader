using Meta.Users;
using Shared;

namespace Meta.Matches;

[GenerateSerializer]
public class MatchSearchResultUpdate : IProjectionPayload
{
    [Id(0)] public required Guid SessionId { get; init; }

    [Id(1)] public required string ServerUrl { get; init; }
    [Id(2)] public required GameMatchType Type { get; init; }

    public INetworkContext ToContext()
    {
        return new SharedMatchmaking.MatchResult()
        {
            SessionId = SessionId,
            ServerUrl = ServerUrl,
            Type = Type
        };
    }
}