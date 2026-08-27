using Shared;

namespace Game.GamePlay;

public class BaseHealthModifierSource : BasePlayerModifierSource
{
    public const string SourceKey = "base_health";

    public BaseHealthModifierSource(int value) : base(PlayerModifier.BaseHealth, value, SourceKey)
    {
    }

    public override void Apply(IPlayer player, MoveSnapshot snapshot)
    {
        player.Health.SetMax(snapshot, (int)Value);
        player.Health.SetCurrent(snapshot, (int)Value);
    }
}
