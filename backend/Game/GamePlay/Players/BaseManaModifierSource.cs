using Shared;

namespace Game.GamePlay;

public class BaseManaModifierSource : BasePlayerModifierSource
{
    public const string SourceKey = "base_mana";

    public BaseManaModifierSource(int value) : base(PlayerModifier.BaseMana, value, SourceKey)
    {
    }

    public override void Apply(IPlayer player, MoveSnapshot snapshot)
    {
        player.Mana.SetMax(snapshot, (int)Value);
        player.Mana.Restore(snapshot);
    }
}
