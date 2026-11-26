using Shared;

namespace Game.GamePlay;

/// <summary>
/// Max mana growth per round: at the start it sits at zero, and at the end of each round
/// it raises the base maximum by 1 (until it hits the cap) and accumulates in Value how much it has given.
/// </summary>
public class BaseManaAddPerRoundModifierSource : BasePlayerModifierSource
{
    public const string SourceKey = "base_mana_add_per_round";

    public BaseManaAddPerRoundModifierSource() : base(PlayerModifier.BaseManaAddPerRound, 0, SourceKey)
    {
    }

    public override void Apply(IPlayer player, MoveSnapshot snapshot)
    {
    }

    public void Grow(IPlayer player, MoveSnapshot snapshot, int cap)
    {
        if (player.Mana.BaseMax >= cap)
            return;

        player.Mana.SetMax(snapshot, player.Mana.BaseMax + 1);
        Value++;
        player.Modifiers.Update(snapshot, this);
    }
}
