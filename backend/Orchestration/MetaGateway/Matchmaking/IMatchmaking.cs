using Shared;

namespace MetaGateway.Matchmaking;

public interface IMatchmaking
{
    Task<SharedMatchmaking.LobbyResult> SearchLobby(Guid userId);

    // The result comes later as a MatchSearchResultUpdate projection with this requestId: Matched, or Failed when
    // the match of the pair could not be created.
    Task SearchMatch(Guid userId, GameMatchType type, Guid requestId);
    Task CancelMatchSearch(Guid userId);
    Task<SharedMatchmaking.MatchResult> Create(Guid userId, GameMatchType type);
    Task<SharedMatchmaking.MatchResult> CreateWithBot(Guid userId, GameMatchType type, AgentMatchFixture? fixture = null);
    MatchmakingStatsDto GetStats();
}
