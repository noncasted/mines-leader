using Shared;

namespace Game.GamePlay;

public class RemoveFlagAction(GameCommandUtils utils) : GameCommand<SharedGameAction.RemoveFlag>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.RemoveFlag request)
    {
        var board = context.Player.Board;
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Fail("Cell is already open");

        if (targetCell.Effects.Any(e => e.Type == CellEffectType.Frost))
            return EmptyResponse.Fail("Cell is frozen");

        var taken = targetCell.ToTaken();

        if (taken.IsFlagged == false)
            return EmptyResponse.Fail("Cell is not flagged");

        taken.RemoveFlag();
        context.Snapshot.RecordFlag(board, request.Position, false);
        context.Snapshot.RecordMines(board, board.MinesScanner.Recalculate(context.Snapshot));

        Utils.SessionLogger.LogFlagRemoved(context.Player.User.Id, request.Position);
        Utils.Stats.Add(context.Player.User.Id, UserStatType.FlagsRemoved);

        return EmptyResponse.Ok;
    }
}