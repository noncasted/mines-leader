using Shared;

namespace Meta.Users;

/// <summary>
/// Пачка статов за один матч. Собирается на игровой сессии и после матча
/// отправляется в грейн игрока одним сообщением.
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
