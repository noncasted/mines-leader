using Infrastructure;
using Meta.Users;
using Shared;

namespace Meta.Matches;

// The record of a match (MatchState), the key is the id of its game session. Written by the game server (GameFlow).
public interface IMatch : IGrainWithGuidKey
{
    [Transaction]
    Task Setup(GameMatchType type, IReadOnlyList<Guid> participants);

    // Gives the match its winner, the ratings and the stats of the players, and closes it.
    [Transaction]
    Task<MatchCompletionSummary> OnComplete(Guid winnerId, Dictionary<Guid, UserStatsDelta> matchStats);

    // Closes a match without a winner; a closed match stays as it is (the end of the session after OnComplete).
    [Transaction]
    Task Close();

    [Transaction]
    Task<MatchState> GetState();
}
