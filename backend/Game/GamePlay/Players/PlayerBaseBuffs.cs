using Shared;

namespace Game.GamePlay;

/// <summary>
/// Zeroes player resources at session start and grants the base through buffs: the values themselves
/// come from the mode config, but the player sees them in the common list of buffs.
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

    /// <summary>End of round: +1 to the base max mana through the growth buff.</summary>
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
