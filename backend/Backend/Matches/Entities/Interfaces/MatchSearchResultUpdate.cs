using Backend.Users;
using Shared;

namespace Backend.Matches;

[GenerateSerializer]
public class MatchSearchResultUpdate : IProjectionPayload
{
    [Id(0)] public required Guid SessionId { get; init; }

    [Id(1)] public required string ServerUrl { get; init; }

    public INetworkContext ToContext()
    {
        return new MatchmakingContexts.GameResult()
        {
            SessionId = SessionId,
            ServerUrl = ServerUrl
        };
    }
}