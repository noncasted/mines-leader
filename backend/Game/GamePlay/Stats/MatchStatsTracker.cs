using Meta.Users;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Accumulates each player's stats for the match. After the match ends the collected
/// deltas go to the achievements grain of the corresponding user.
/// </summary>
public interface IMatchStatsTracker
{
    void Add(Guid userId, UserStatType type, long amount = 1);

    void AddCardPlayed(Guid userId, CardGroup group);

    /// <summary>Takes the accumulated stats and clears the tracker: on a rematch the count starts over.</summary>
    Dictionary<Guid, UserStatsDelta> Collect();
}

public class MatchStatsTracker : IMatchStatsTracker
{
    private readonly Dictionary<Guid, UserStatsDelta> _deltas = new();

    public void Add(Guid userId, UserStatType type, long amount = 1)
    {
        if (amount <= 0)
            return;

        GetOrCreate(userId).Add(type, amount);
    }

    public void AddCardPlayed(Guid userId, CardGroup group)
    {
        GetOrCreate(userId).AddCardPlayed(group);
    }

    public Dictionary<Guid, UserStatsDelta> Collect()
    {
        var result = _deltas.ToDictionary(pair => pair.Key, pair => pair.Value);
        _deltas.Clear();
        return result;
    }

    private UserStatsDelta GetOrCreate(Guid userId)
    {
        if (_deltas.TryGetValue(userId, out var delta) == false)
        {
            delta = new UserStatsDelta();
            _deltas[userId] = delta;
        }

        return delta;
    }
}
