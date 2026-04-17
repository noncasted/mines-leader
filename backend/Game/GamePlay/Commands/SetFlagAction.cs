using Shared;

namespace Game.GamePlay;

public class SetFlagAction(GameCommandUtils utils) : GameCommand<SharedGameAction.SetFlag>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.SetFlag request)
    {
        var board = context.Player.Board;
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Failed;

        if (targetCell.Effects.Any(e => e.Type == CellEffectType.Frost))
            return EmptyResponse.Fail("Cell is frozen");

        var taken = targetCell.ToTaken();

        if (taken.IsFlagged == true)
            return EmptyResponse.Failed;

        taken.SetFlag();
        context.Snapshot.RecordFlag(board, request.Position, true);
        context.Snapshot.RecordMines(board, board.MinesScanner.Recalculate(context.Snapshot));

        Utils.SessionLogger.LogFlagSet(context.Player.User.Id, request.Position);

        return EmptyResponse.Ok;
    }
}