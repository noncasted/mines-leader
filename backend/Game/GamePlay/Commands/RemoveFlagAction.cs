using Shared;

namespace Game.GamePlay;

public class RemoveFlagAction(GameCommandUtils utils) : GameCommand<SharedGameAction.RemoveFlag>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.RemoveFlag request)
    {
        var board = context.Player.Board;
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Failed;

        var taken = targetCell.ToTaken();

        if (taken.IsFlagged == false)
            return EmptyResponse.Failed;

        taken.RemoveFlag();

        return EmptyResponse.Ok;
    }
}