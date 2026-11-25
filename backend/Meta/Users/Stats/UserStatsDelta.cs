using Shared;

namespace Meta.Users;

/// <summary>
/// A batch of stats for one match. Collected in the game session and sent to
/// the player's grain in one message after the match.
/// </summary>
[GenerateSerializer]
public class UserStatsDelta
{
    [Id(0)] public Dictionary<UserStatType, long> Counters { get; set; } = new();
    [Id(1)] public Dictionary<CardGroup, long> CardsPlayedByGroup { get; set; } = new();

    public bool IsEmpty => Counters.Count == 0 && CardsPlayedByGroup.Count == 0;

    public void Add(UserStatType type, long amount = 1)
    {
        if (amount == 0)
            return;

        Counters[type] = Counters.GetValueOrDefault(type) + amount;
    }

    public void AddCardPlayed(CardGroup group, long amount = 1)
    {
        if (amount == 0)
            return;

        CardsPlayedByGroup[group] = CardsPlayedByGroup.GetValueOrDefault(group) + amount;
    }

    public long Get(UserStatType type) => Counters.GetValueOrDefault(type);

    public static UserStatsDelta Single(UserStatType type, long amount = 1)
    {
        var delta = new UserStatsDelta();
        delta.Add(type, amount);
        return delta;
    }
}
