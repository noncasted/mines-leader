using Shared;

namespace Game.GamePlay;

/// <summary>
/// Обнуляет ресурсы игрока на старте сессии и выдаёт базу бафами: сами значения
/// приходят из конфига режима, но игрок видит их в общем списке бафов.
/// </summary>
public static class PlayerBaseBuffs
{
    public static void Grant(IPlayer player, MoveSnapshot snapshot, int health, int moves, int mana)
    {
        player.Health.SetMax(snapshot, 0);
        player.Health.SetCurrent(snapshot, 0);
        player.Moves.SetMax(snapshot, 0);
        player.Mana.SetMax(snapshot, 0);
        player.Mana.SetCurrent(snapshot, 0);

        Grant(player, snapshot, new BaseHealthModifierSource(health));
        Grant(player, snapshot, new BaseMovesModifierSource(moves));
        Grant(player, snapshot, new BaseManaModifierSource(mana));
        Grant(player, snapshot, new BaseManaAddPerRoundModifierSource());
    }

    /// <summary>Конец раунда: +1 к базовому максимуму маны через баф прироста.</summary>
    public static void GrowMana(IPlayer player, MoveSnapshot snapshot, int cap)
    {
        var source = player.Modifiers.Sources.OfType<BaseManaAddPerRoundModifierSource>().FirstOrDefault();

        if (source == null)
        {
            source = new BaseManaAddPerRoundModifierSource();
            Grant(player, snapshot, source);
        }

        source.Grow(player, snapshot, cap);
    }

    private static void Grant(IPlayer player, MoveSnapshot snapshot, BasePlayerModifierSource source)
    {
        player.Modifiers.Add(snapshot, source);
        source.Apply(player, snapshot);
    }
}
