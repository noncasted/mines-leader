using System.Collections.Concurrent;
using Game.MatchMetrics;

namespace Framework.Tests.MatchMetrics;

// The admission of a game for the tests: the players and the close of each match set by hand.
public sealed class TestMatchAdmission : IMatchMetricsAdmission
{
    private readonly ConcurrentDictionary<Guid, (HashSet<Guid> Players, DateTime? ClosedAt)> _matches = new();

    public int Checks { get; private set; }

    public void Add(Guid matchId, params Guid[] players)
    {
        _matches[matchId] = (players.ToHashSet(), null);
    }

    public void Close(Guid matchId, DateTime at)
    {
        _matches[matchId] = (_matches[matchId].Players, at);
    }

    public Task<MatchMetricsAdmit> Check(Guid matchId, Guid userId)
    {
        Checks++;

        if (_matches.TryGetValue(matchId, out var match) == false)
            return Task.FromResult(new MatchMetricsAdmit(false, null));

        return Task.FromResult(new MatchMetricsAdmit(match.Players.Contains(userId), match.ClosedAt));
    }
}
