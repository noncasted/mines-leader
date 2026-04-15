using Shared;

namespace Meta.Users;

public static class LootRewardPicker
{
    public static List<CardType> Pick(IReadOnlyList<CardType> ownedCards, int count)
    {
        var ownedSet = new HashSet<CardType>(ownedCards);

        var pool = CardTypeExtensions.All
                                     .Where(c => !ownedSet.Contains(c))
                                     .Where(c => c != CardType.Dud)
                                     .ToList();

        if (pool.Count == 0)
            return new List<CardType>();

        var random = new Random();

        return pool.OrderBy(_ => random.Next())
                   .Take(Math.Min(count, pool.Count))
                   .ToList();
    }
}