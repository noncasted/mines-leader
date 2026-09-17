using Shared;

namespace Game.GamePlay;

/// <summary>
/// Прирост максимальной маны за раунды: на старте висит с нулём, а в конце каждого раунда
/// поднимает базовый максимум на 1 (пока не упёрлись в кап) и копит в Value, сколько уже дал.
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
