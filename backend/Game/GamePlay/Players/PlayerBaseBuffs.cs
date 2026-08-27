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
    }

    private static void Grant(IPlayer player, MoveSnapshot snapshot, BasePlayerModifierSource source)
    {
        player.Modifiers.Add(snapshot, source);
        source.Apply(player, snapshot);
    }
}
