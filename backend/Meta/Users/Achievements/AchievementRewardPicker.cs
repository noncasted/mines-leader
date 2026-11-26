using Shared;

namespace Meta.Users;

public static class AchievementRewardPicker
{
    /// <summary>
    /// A set of cards for the player to choose from. Returns fewer than <paramref name="count"/>
    /// if fewer suitable cards remain.
    /// </summary>
    public static IReadOnlyList<CardType> PickCards(
        CardConfigOptions cardConfigs,
        IReadOnlyCollection<CardType> ownedCards,
        IReadOnlyCollection<CardGroup> possibleGroups,
        Random random,
        int count)
    {
        var pool = BuildPool(cardConfigs, ownedCards, possibleGroups);
        var picked = new List<CardType>(Math.Min(count, pool.Count));

        while (picked.Count < count && pool.Count > 0)
        {
            var index = random.Next(pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return picked;
    }

    private static List<CardType> BuildPool(
        CardConfigOptions cardConfigs,
        IReadOnlyCollection<CardType> ownedCards,
        IReadOnlyCollection<CardGroup> possibleGroups)
    {
        var ownedSet = new HashSet<CardType>(ownedCards);
        var all = cardConfigs.All;

        return CardTypeExtensions.All
                                 .Where(card => ownedSet.Contains(card) == false)
                                 .Where(card => card != CardType.Dud)
                                 .Where(card => all.ContainsKey(card))
                                 .Where(card => possibleGroups == null ||
                                                possibleGroups.Count == 0 ||
                                                possibleGroups.Contains(all[card].Group))
                                 .ToList();
    }
}
