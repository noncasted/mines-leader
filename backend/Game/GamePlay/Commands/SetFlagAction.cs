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

        var taken = targetCell.ToTaken();

        if (taken.IsFlagged == true)
            return EmptyResponse.Failed;

        taken.SetFlag();
        board.OnUpdated();

        return EmptyResponse.Ok;
    }
}