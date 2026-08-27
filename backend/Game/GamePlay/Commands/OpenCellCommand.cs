using Game.GamePlay.Snapshots;
using Shared;

namespace Game.GamePlay;

public class OpenCellCommand(GameCommandUtils utils) : GameCommand<SharedGameAction.Open>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.Open request)
    {
        var board = context.Player.Board;
        board.EnsureGenerated(request.Position);
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Failed;

        if (targetCell.Effects.Any(e => e.Type == CellEffectType.Frost))
            return EmptyResponse.Fail("Cell is frozen");

        var hasMine = targetCell.ToTaken().HasMine == true;
        var shieldConsumed = false;

        if (hasMine)
        {
            var shield = (int)context.Player.Modifiers.Get(PlayerModifier.Shield);

            if (shield > 0)
            {
                context.Player.Modifiers.RemoveOne(context.Snapshot, PlayerModifier.Shield);
                shieldConsumed = true;
            }
            else
            {
                context.Player.Health.TakeDamage(context.Snapshot, 1);

                var soulLink = (int)context.Player.Modifiers.Get(PlayerModifier.SoulLink);

                if (soulLink > 0)
                {
                    var opponent = Utils.GameContext.GetOpponent(context.Player);
                    opponent.Health.TakeDamage(context.Snapshot, 1);
                }
            }

            context.Snapshot.RecordExplosion(board, request.Position);
            targetCell.ToTaken().Explode();
            targetCell.ToFree();
            board.RegisterDetonatedMine();
            context.Snapshot.RecordCellFree(board, request.Position);
        }

        Utils.SessionLogger.LogCellOpened(context.Player.User.Id, request.Position, hasMine, shieldConsumed);

        var userId = context.Player.User.Id;
        Utils.Stats.Add(userId, UserStatType.CellsOpened);

        if (hasMine)
        {
            Utils.Stats.Add(userId, UserStatType.MinesDetonated);

            if (shieldConsumed == false)
                Utils.Stats.Add(userId, UserStatType.DamageTaken);
        }

        context.Player.Actions.OnCellOpened();
        context.Player.Moves.OnUsed(context.Snapshot);

        context.Snapshot.RecordReveal(board, request.Position);

        return EmptyResponse.Ok;
    }
}