using Shared;

namespace Game.GamePlay;

public partial class MoveSnapshot
{
    public void RecordManaUpdate(IPlayer player)
    {
        var mana = player.Mana;

        AppendResource(player.User.Id, ResourceKind.Mana, new PlayerSnapshotRecord.ManaUpdate
        {
            PlayerId = player.User.Id,
            Current = mana.Current,
            BaseMax = mana.BaseMax,
            ResultMax = mana.ResultMax
        });
    }

    public void RecordHealthUpdate(IPlayer player)
    {
        var health = player.Health;

        AppendResource(player.User.Id, ResourceKind.Health, new PlayerSnapshotRecord.HealthUpdate
        {
            PlayerId = player.User.Id,
            Current = health.Current,
            BaseMax = health.BaseMax,
            ResultMax = health.ResultMax
        });
    }

    public void RecordMovesUpdate(IPlayer player)
    {
        var moves = player.Moves;

        AppendResource(player.User.Id, ResourceKind.Moves, new PlayerSnapshotRecord.MovesUpdate
        {
            PlayerId = player.User.Id,
            Left = moves.Left,
            BaseMax = moves.BaseMax,
            ResultMax = moves.ResultMax,
            IsAvailable = moves.IsAvailable
        });
    }

    public void RecordModifierUpdate(IPlayer player, DurationalModifierOverview overview)
    {
        Append(new PlayerSnapshotRecord.ModifierUpdate
        {
            PlayerId = player.User.Id,
            Overview = overview
        });
    }
}
