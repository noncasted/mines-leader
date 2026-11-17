using Shared;

namespace Game.GamePlay;

/// <summary>
/// Итоговая стоимость карты с учётом модификаторов игрока. Один расчёт для розыгрыша,
/// legal plays и наблюдения агента, чтобы "cost > mana" у агента совпадало с отказом сервера.
/// </summary>
public static class CardManaCost
{
    public static int Resolve(IPlayer player, ICardConfig config)
    {
        var cost = config.ManaCost;
        var nextDiscount = (int)player.Modifiers.Get(PlayerModifier.NextCardDiscount);

        if (nextDiscount > 0)
            cost -= nextDiscount;

        cost -= (int)player.Modifiers.Get(PlayerModifier.AllCardsDiscount);
        cost += (int)player.Modifiers.Get(PlayerModifier.ManaCostPenalty);

        return Math.Max(0, cost);
    }

    public static string NotEnough(int cost, int current)
    {
        return $"Not enough mana: {cost} needed, {current} left";
    }
}
