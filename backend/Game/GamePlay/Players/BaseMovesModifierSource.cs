using Shared;

namespace Game.GamePlay;

public class BaseMovesModifierSource : BasePlayerModifierSource
{
    public const string SourceKey = "base_moves";

    public BaseMovesModifierSource(int value) : base(PlayerModifier.BaseMoves, value, SourceKey)
    {
    }

    public override void Apply(IPlayer player, MoveSnapshot snapshot)
    {
        player.Moves.SetMax(snapshot, (int)Value);
    }
}
